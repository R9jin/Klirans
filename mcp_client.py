import sys
import json
import urllib.request

URL = "http://127.0.0.1:8080/mcp"
HEADERS = {
    "Accept": "application/json, text/event-stream",
    "Content-Type": "application/json"
}

def send_request(session_id, method, params, req_id):
    req_headers = dict(HEADERS)
    if session_id:
        req_headers["mcp-session-id"] = session_id
    body = json.dumps({
        "jsonrpc": "2.0",
        "method": method,
        "params": params,
        "id": req_id
    }).encode("utf-8")
    req = urllib.request.Request(URL, data=body, headers=req_headers)
    with urllib.request.urlopen(req, timeout=30) as resp:
        while True:
            line = resp.readline()
            if not line:
                break
            decoded = line.decode("utf-8", errors="replace").strip()
            if decoded.startswith("data:"):
                payload = json.loads(decoded[5:].strip())
                if "method" in payload:
                    continue  # skip notifications
                return payload
    return None

def call_tool(tool_name: str, arguments: dict = None):
    if arguments is None:
        arguments = {}
    
    # 1. Initialize
    init_body = json.dumps({
        "jsonrpc": "2.0",
        "method": "initialize",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "mcp-client", "version": "1.0"}
        },
        "id": 1
    }).encode("utf-8")
    
    init_req = urllib.request.Request(URL, data=init_body, headers=HEADERS)
    with urllib.request.urlopen(init_req) as resp:
        session_id = resp.headers.get("mcp-session-id")
    
    if not session_id:
        raise RuntimeError("Failed to obtain mcp-session-id")

    # 2. Automatically select instance if needed
    if tool_name != "set_active_instance":
        res_data = send_request(session_id, "resources/read", {"uri": "mcpforunity://instances"}, 2)
        if res_data and "result" in res_data:
            contents = res_data["result"].get("contents", [])
            if contents:
                inst_info = json.loads(contents[0].get("text", "{}"))
                instances = inst_info.get("instances", [])
                if instances:
                    h = instances[0].get("hash")
                    if h:
                        send_request(session_id, "tools/call", {"name": "set_active_instance", "arguments": {"instance": h}}, 3)

    # 3. Call tool
    res = send_request(session_id, "tools/call", {"name": tool_name, "arguments": arguments}, 4)
    if res:
        if "error" in res:
            print(json.dumps({"success": False, "error": res["error"]}, indent=2))
        else:
            result = res.get("result", {})
            print(json.dumps(result, indent=2))
    else:
        print(json.dumps({"success": False, "error": "No response received"}, indent=2))

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python mcp_client.py <tool_name> [args_json_or_@file]")
        sys.exit(1)
    
    name = sys.argv[1]
    args = {}
    if len(sys.argv) > 2:
        arg_str = sys.argv[2]
        if arg_str.startswith("@"):
            with open(arg_str[1:], "r", encoding="utf-8") as f:
                args = json.load(f)
        else:
            try:
                args = json.loads(arg_str)
            except Exception:
                args = json.loads(arg_str.replace("'", '"'))
    
    call_tool(name, args)
