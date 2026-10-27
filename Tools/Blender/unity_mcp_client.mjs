// Project-local MCP client: uses the shipped Unity MCP server and its authentication file.
import { Client } from '../../Packages/com.gamelovers.mcp-unity/Server~/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import { StdioClientTransport } from '../../Packages/com.gamelovers.mcp-unity/Server~/node_modules/@modelcontextprotocol/sdk/dist/esm/client/stdio.js';
const root='D:/Texas-Holdem';
const client=new Client({name:'codex-harbor-import',version:'1.0.0'},{capabilities:{}});
const transport=new StdioClientTransport({command:process.execPath,args:[root+'/Packages/com.gamelovers.mcp-unity/Server~/build/index.js'],cwd:root,env:{...process.env,MCP_UNITY_SETTINGS_PATH:root+'/ProjectSettings/McpUnitySettings.json',MCP_UNITY_AUTH_TOKEN_PATH:root+'/Library/McpUnity/bridge-token'},stderr:'pipe'});
try {
 await client.connect(transport);
 const name=process.argv[2]||'list';
 const result=name==='list'?await client.listTools():await client.callTool({name,arguments:JSON.parse(process.argv[3]||'{}')},undefined,{timeout:180000});
 console.log(JSON.stringify(result));
} finally {await client.close();}
