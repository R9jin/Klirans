using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HamAndCheeseVendorUI — Interactive dialogue and shop window for the wandering
/// Ham and Cheese student vendor ("Naglalako ng Ham and Cheese").
/// Allows buying to eat immediately (for instant anxiety relief) or storing in bag.
/// </summary>
public class HamAndCheeseVendorUI : MonoBehaviour
{
    public static HamAndCheeseVendorUI Instance { get; private set; }

    private GameObject _panelRoot;
    private Text _vendorNameText;
    private Text _dialogueText;
    private Text _priceText;
    private Text _coinBalanceText;

    private Button _buyAndEatBtn;
    private Text _buyAndEatBtnText;
    private Button _buyAndStoreBtn;
    private Text _buyAndStoreBtnText;
    private Button _leaveBtn;

    private HamAndCheeseVendor _activeVendor;
    private bool _isOpen = false;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        EnsureUIConstructed();
    }

    private void Update()
    {
        if (!_isOpen) return;

        // Keyboard hotkeys: 1 = Buy & Eat, 2 = Buy & Store, Esc or E = Leave
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            OnBuyAndEatClicked();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            OnBuyAndStoreClicked();
        }
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Alpha3))
        {
            CloseUI();
        }
    }

    public void OpenUI(HamAndCheeseVendor vendor)
    {
        EnsureUIConstructed();
        _activeVendor = vendor;
        _isOpen = true;

        if (_panelRoot != null) _panelRoot.SetActive(true);

        // Lock player movement and unlock cursor during vendor interaction
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var move = player.GetComponent<PlayerMovement>();
            if (move != null) move.SetControlsEnabled(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshDisplay();
    }

    public void CloseUI()
    {
        _isOpen = false;
        if (_panelRoot != null) _panelRoot.SetActive(false);

        // Restore player movement and cursor
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var move = player.GetComponent<PlayerMovement>();
            if (move != null) move.SetControlsEnabled(true);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _activeVendor = null;
    }

    private void RefreshDisplay()
    {
        if (_activeVendor == null) return;

        int price = _activeVendor.CurrentPrice;
        int coins = CoinManager.Instance != null ? CoinManager.Instance.CurrentCoins : 0;
        bool hasEnough = coins >= price;
        bool alreadyBought = _activeVendor.HasBoughtThisEncounter;

        if (_coinBalanceText != null)
        {
            _coinBalanceText.text = $"Mayroon kang: ₱{coins}";
        }

        if (_priceText != null)
        {
            _priceText.text = $"Presyo: ₱{price}";
        }

        if (alreadyBought)
        {
            if (_dialogueText != null)
                _dialogueText.text = "\"Salamat paps! Naubusan na ako ng baon for now, iikot muna ako sa ibang floor! Ingat sa clearance!\"";

            if (_buyAndEatBtn != null) _buyAndEatBtn.interactable = false;
            if (_buyAndStoreBtn != null) _buyAndStoreBtn.interactable = false;
            if (_buyAndEatBtnText != null) _buyAndEatBtnText.text = "[Naubusan ng Stock]";
            if (_buyAndStoreBtnText != null) _buyAndStoreBtnText.text = "[Sold Out]";
        }
        else if (!hasEnough)
        {
            if (_dialogueText != null)
                _dialogueText.text = $"\"Bili na paps, mainit-init pang Ham & Cheese... Kaso kulang ang barya mo! Kailangan mo ng ₱{price}. Maghanap ka muna sa mga classroom at desk!\"";

            if (_buyAndEatBtn != null) _buyAndEatBtn.interactable = false;
            if (_buyAndStoreBtn != null) _buyAndStoreBtn.interactable = false;
            if (_buyAndEatBtnText != null) _buyAndEatBtnText.text = $"[1] Kulang Barya (₱{price})";
            if (_buyAndStoreBtnText != null) _buyAndStoreBtnText.text = $"[2] Kulang Barya (₱{price})";
        }
        else
        {
            if (_dialogueText != null)
                _dialogueText.text = "\"Bili na kayo, mainit-init pang Ham and Cheese! Pampalubag-loob sa clearance! Mababawasan kaba at anxiety mo dito!\"";

            if (_buyAndEatBtn != null) _buyAndEatBtn.interactable = true;
            if (_buyAndStoreBtn != null) _buyAndStoreBtn.interactable = true;
            if (_buyAndEatBtnText != null) _buyAndEatBtnText.text = $"[1] Bumili at Kainin Agad (-30 Anxiety)";
            if (_buyAndStoreBtnText != null) _buyAndStoreBtnText.text = $"[2] Bumili at Itabi sa Bag";
        }
    }

    private void OnBuyAndEatClicked()
    {
        if (_activeVendor == null || _activeVendor.HasBoughtThisEncounter) return;

        int price = _activeVendor.CurrentPrice;
        if (CoinManager.Instance == null || !CoinManager.Instance.SpendCoins(price))
        {
            RefreshDisplay();
            return;
        }

        _activeVendor.RegisterPurchase();

        // Consume immediately
        HamAndCheeseItem.Consume(30f, 25f);

        if (_dialogueText != null)
            _dialogueText.text = "\"Salamat paps! Kain mabuti! Masarap 'yan pampakalma, kaya mo matapos ang clearance!\"";

        RefreshDisplay();
    }

    private void OnBuyAndStoreClicked()
    {
        if (_activeVendor == null || _activeVendor.HasBoughtThisEncounter) return;

        // Check if inventory has room
        var sandwichAsset = _activeVendor.sandwichItemAsset;
        if (sandwichAsset == null)
        {
            sandwichAsset = HamAndCheeseItem.Instance?.sandwichItemAsset;
        }

        if (InventoryManager.Instance != null && !InventoryManager.Instance.HasFreeSlot())
        {
            if (_dialogueText != null)
                _dialogueText.text = "\"Puno na ang bag mo paps! Kainin mo na lang agad o magbawas ka muna ng gamit.\"";
            return;
        }

        int price = _activeVendor.CurrentPrice;
        if (CoinManager.Instance == null || !CoinManager.Instance.SpendCoins(price))
        {
            RefreshDisplay();
            return;
        }

        _activeVendor.RegisterPurchase();

        if (sandwichAsset != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(sandwichAsset, 1);
        }

        if (_dialogueText != null)
            _dialogueText.text = "\"Salamat paps! Itinago ko na sa bag mo. Kainin mo 'yan pag mataas na anxiety mo!\"";

        RefreshDisplay();
    }

    private void EnsureUIConstructed()
    {
        if (_panelRoot != null) return;

        var hud = GameObject.Find("HudCanvas");
        if (hud == null) return;

        Font horrorFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if UNITY_EDITOR
        var loadedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/watch people die.ttf");
        if (loadedFont != null) horrorFont = loadedFont;
#endif

        // Root Modal Panel
        _panelRoot = new GameObject("HamAndCheeseVendorModal", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        _panelRoot.transform.SetParent(hud.transform, false);

        var rt = _panelRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(620f, 320f);

        var bg = _panelRoot.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.07f, 0.06f, 0.94f); // Rich dark mahogany frame

        var outline = _panelRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.92f, 0.72f, 0.22f, 0.75f); // Golden toasted outline
        outline.effectDistance = new Vector2(2f, -2f);

        // Header: NPC Name
        var headerGO = new GameObject("VendorHeader", typeof(RectTransform), typeof(Text));
        headerGO.transform.SetParent(_panelRoot.transform, false);
        var hRT = headerGO.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = new Vector2(0f, -16f);
        hRT.sizeDelta = new Vector2(-40f, 32f);

        _vendorNameText = headerGO.GetComponent<Text>();
        _vendorNameText.text = "RENZ — NAGLALAKO NG HAM AND CHEESE";
        _vendorNameText.font = horrorFont;
        _vendorNameText.fontSize = 20;
        _vendorNameText.fontStyle = FontStyle.Bold;
        _vendorNameText.color = new Color(1.0f, 0.85f, 0.30f, 1f);
        _vendorNameText.alignment = TextAnchor.MiddleCenter;

        // Sub-header info: Price & Coin balance
        var priceGO = new GameObject("PriceInfo", typeof(RectTransform), typeof(Text));
        priceGO.transform.SetParent(_panelRoot.transform, false);
        var pRT = priceGO.GetComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0f, 1f);
        pRT.anchorMax = new Vector2(0.5f, 1f);
        pRT.pivot = new Vector2(0f, 1f);
        pRT.anchoredPosition = new Vector2(28f, -50f);
        pRT.sizeDelta = new Vector2(260f, 24f);

        _priceText = priceGO.GetComponent<Text>();
        _priceText.text = "Presyo: ₱3";
        _priceText.font = horrorFont;
        _priceText.fontSize = 16;
        _priceText.color = new Color(0.95f, 0.90f, 0.65f, 1f);
        _priceText.alignment = TextAnchor.MiddleLeft;

        var coinsGO = new GameObject("CoinsInfo", typeof(RectTransform), typeof(Text));
        coinsGO.transform.SetParent(_panelRoot.transform, false);
        var cRT = coinsGO.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0.5f, 1f);
        cRT.anchorMax = new Vector2(1f, 1f);
        cRT.pivot = new Vector2(1f, 1f);
        cRT.anchoredPosition = new Vector2(-28f, -50f);
        cRT.sizeDelta = new Vector2(260f, 24f);

        _coinBalanceText = coinsGO.GetComponent<Text>();
        _coinBalanceText.text = "Mayroon kang: ₱0";
        _coinBalanceText.font = horrorFont;
        _coinBalanceText.fontSize = 16;
        _coinBalanceText.color = new Color(0.40f, 1.0f, 0.50f, 1f);
        _coinBalanceText.alignment = TextAnchor.MiddleRight;

        // Dialogue Body
        var bodyGO = new GameObject("DialogueBody", typeof(RectTransform), typeof(Text));
        bodyGO.transform.SetParent(_panelRoot.transform, false);
        var bRT = bodyGO.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 1f);
        bRT.anchorMax = new Vector2(1f, 1f);
        bRT.pivot = new Vector2(0.5f, 1f);
        bRT.anchoredPosition = new Vector2(0f, -84f);
        bRT.sizeDelta = new Vector2(-56f, 68f);

        _dialogueText = bodyGO.GetComponent<Text>();
        _dialogueText.text = "\"Bili na kayo, mainit-init pang Ham and Cheese! Pampalubag-loob sa clearance!\"";
        _dialogueText.font = horrorFont;
        _dialogueText.fontSize = 16;
        _dialogueText.color = new Color(0.92f, 0.90f, 0.88f, 1f);
        _dialogueText.alignment = TextAnchor.UpperLeft;

        // Buttons container
        float btnY = -170f;
        float btnHeight = 36f;
        float btnSpacing = 44f;

        // Button 1: Buy & Eat
        var btn1 = CreateButton("BuyAndEatBtn", _panelRoot.transform, new Vector2(0f, btnY), new Vector2(-56f, btnHeight), horrorFont, "[1] Bumili at Kainin Agad (-30 Anxiety)");
        _buyAndEatBtn = btn1.GetComponent<Button>();
        _buyAndEatBtnText = btn1.GetComponentInChildren<Text>();
        _buyAndEatBtn.onClick.AddListener(OnBuyAndEatClicked);

        // Button 2: Buy & Store
        var btn2 = CreateButton("BuyAndStoreBtn", _panelRoot.transform, new Vector2(0f, btnY - btnSpacing), new Vector2(-56f, btnHeight), horrorFont, "[2] Bumili at Itabi sa Bag");
        _buyAndStoreBtn = btn2.GetComponent<Button>();
        _buyAndStoreBtnText = btn2.GetComponentInChildren<Text>();
        _buyAndStoreBtn.onClick.AddListener(OnBuyAndStoreClicked);

        // Button 3: Leave
        var btn3 = CreateButton("LeaveBtn", _panelRoot.transform, new Vector2(0f, btnY - btnSpacing * 2f), new Vector2(-56f, btnHeight), horrorFont, "[E / Esc] Huwag na muna");
        _leaveBtn = btn3.GetComponent<Button>();
        _leaveBtn.onClick.AddListener(CloseUI);

        _panelRoot.SetActive(false);
    }

    private GameObject CreateButton(string name, Transform parent, Vector2 pos, Vector2 size, Font font, string label)
    {
        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = btnGO.GetComponent<Image>();
        img.color = new Color(0.18f, 0.16f, 0.14f, 0.95f);

        var btn = btnGO.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.35f, 0.30f, 0.20f, 1f);
        colors.pressedColor = new Color(0.50f, 0.40f, 0.15f, 1f);
        colors.disabledColor = new Color(0.10f, 0.10f, 0.10f, 0.5f);
        btn.colors = colors;

        var outline = btnGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.60f, 0.50f, 0.25f, 0.50f);
        outline.effectDistance = new Vector2(1f, -1f);

        var txtGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(btnGO.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        var txt = txtGO.GetComponent<Text>();
        txt.text = label;
        txt.font = font;
        txt.fontSize = 15;
        txt.fontStyle = FontStyle.Bold;
        txt.color = new Color(1.0f, 0.95f, 0.85f, 1f);
        txt.alignment = TextAnchor.MiddleCenter;

        return btnGO;
    }
}
