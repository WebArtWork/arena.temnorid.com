import asyncio, os
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

async def main():
    params = StdioServerParameters(
        command=r'C:\Users\Unity\.local\bin\uvx.exe',
        args=['mcp-for-blender'],
        env={**os.environ, 'DISABLE_TELEMETRY':'true', 'PYTHONUTF8':'1'},
    )
    async with stdio_client(params) as (reader, writer):
        async with ClientSession(reader, writer) as session:
            info=await session.initialize()
            print('SERVER', info.serverInfo)
            listing=await session.list_tools()
            print('TOOLS', len(listing.tools))
            result=await session.call_tool('get_scene_info', {})
            print('SCENE', result.model_dump_json())

asyncio.run(main())
