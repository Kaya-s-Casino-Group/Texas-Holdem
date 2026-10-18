#!/usr/bin/env python3
"""Loopback-only editor host. No writes to project assets; semantic validation is C#."""
import argparse
import json
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
MODULES = ('Core', 'Abilities', 'Combat', 'Scoring', 'Holdem', 'Draw', 'Runs', 'Progression', 'Encounters', 'Composition')
MAX_BODY = 4 * 1024 * 1024  # UTF-8 transport limit; reader additionally limits characters.


def build(output):
    compiler = os.environ.get('POKER_CSC', 'mcs')
    if not shutil.which(compiler):
        raise RuntimeError('找不到 C# 编译器 mcs，请安装 Mono，或设置 POKER_CSC。')
    for module in MODULES:
        dependencies = {
            'Abilities': ['Core'], 'Scoring': ['Core'], 'Combat': ['Core', 'Abilities'],
            'Holdem': ['Core', 'Abilities'], 'Draw': ['Core', 'Abilities', 'Scoring'],
            'Runs': ['Core', 'Abilities', 'Scoring'], 'Progression': ['Core'], 'Encounters': ['Core'],
            'Composition': list(MODULES[:-1]),
        }.get(module, [])
        sources = sorted((ROOT / 'Assets/Scripts/Rules/PokerEngine/Runtime' / module).rglob('*.cs'))
        subprocess.run([compiler, '-langversion:7.2', '-warnaserror', '-target:library', '-r:System.Runtime.Serialization',
                        *['-r:' + str(output / ('PokerEngine.' + dep + '.dll')) for dep in dependencies],
                        '-out:' + str(output / ('PokerEngine.' + module + '.dll')), *map(str, sources)], check=True)
    bridge = output / 'CompilerBridge.exe'
    subprocess.run([compiler, '-langversion:7.2', '-warnaserror', '-r:System.Web.Extensions',
                    *['-r:' + str(output / ('PokerEngine.' + module + '.dll')) for module in MODULES],
                    '-out:' + str(bridge), str(HERE / 'CompilerBridge.cs')], check=True)
    return bridge


class Compiler:
    def __init__(self, bridge):
        self.bridge = bridge
        self.runtime = os.environ.get('POKER_MONO', 'mono')
        self.slots = threading.BoundedSemaphore(2)

    def call(self, xml=None):
        if not self.slots.acquire(timeout=1):
            raise RuntimeError('校验器忙碌，请稍后重试。')
        try:
            result = subprocess.run([self.runtime, str(self.bridge)] + (['--metadata'] if xml is None else []),
                                    input=xml, encoding='utf-8', capture_output=True, timeout=25)
            if result.returncode:
                raise RuntimeError('C# 校验器执行失败：' + result.stderr[:1200])
            return json.loads(result.stdout.lstrip('\ufeff'))
        finally:
            self.slots.release()


def make_server(compiler, port=18765):
    token = secrets.token_urlsafe(32)
    metadata = compiler.call()
    files = {'/': ('index.html', 'text/html'), '/app.js': ('app.js', 'text/javascript'),
             '/schema.js': ('schema.js', 'text/javascript'), '/model.js': ('model.js', 'text/javascript'),
             '/style.css': ('style.css', 'text/css')}

    class Handler(BaseHTTPRequestHandler):
        def reply(self, code, data, content_type='application/json'):
            body = json.dumps(data, ensure_ascii=False).encode('utf-8') if content_type == 'application/json' else data
            self.send_response(code)
            self.send_header('Content-Type', content_type + '; charset=utf-8')
            self.send_header('Content-Length', str(len(body)))
            self.send_header('Cache-Control', 'no-store')
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.send_header('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'")
            self.end_headers()
            self.wfile.write(body)

        def valid_host(self):
            return self.headers.get('Host') == '127.0.0.1:' + str(self.server.server_port)

        def do_GET(self):
            if not self.valid_host():
                self.reply(403, {'error': '请使用启动器给出的 127.0.0.1 地址。'}); return
            route = urlsplit(self.path).path
            if route == '/api/bootstrap':
                self.reply(200, {'token': token, 'metadata': metadata}); return
            if route == '/api/example':
                path = ROOT / 'Assets/Scripts/Rules/PokerEngine/Runtime/Composition/Content/Examples/battle-content.xml'
                self.reply(200, path.read_bytes(), 'application/xml'); return
            if route in files:
                filename, mime = files[route]
                self.reply(200, (HERE / 'web' / filename).read_bytes(), mime); return
            self.reply(404, {'error': '未找到页面。'})

        def do_POST(self):
            origin = 'http://127.0.0.1:' + str(self.server.server_port)
            if not self.valid_host() or self.headers.get('Origin', origin) != origin or not secrets.compare_digest(self.headers.get('X-Editor-Token', ''), token):
                self.reply(403, {'error': '请求来源无效，请刷新编辑器页面。'}); return
            if self.path != '/api/validate':
                self.reply(404, {'error': '未知接口。'}); return
            try:
                length = int(self.headers.get('Content-Length', '0'))
                if not 0 < length <= MAX_BODY:
                    self.reply(413, {'error': 'XML 为空或超过传输大小限制。'}); return
                if self.headers.get('Content-Type', '').split(';')[0] != 'application/xml':
                    self.reply(415, {'error': '需要 application/xml。'}); return
                self.connection.settimeout(30)
                xml = self.rfile.read(length).decode('utf-8')
                self.reply(200, compiler.call(xml))
            except (ValueError, UnicodeError):
                self.reply(400, {'error': '请求长度或 UTF-8 编码无效。'})
            except (RuntimeError, subprocess.TimeoutExpired, OSError) as error:
                self.reply(503, {'error': '校验服务不可用。' + str(error)})

        def log_message(self, fmt, *args):
            # Paths/status only; never log submitted XML.
            pass

    return ThreadingHTTPServer(('127.0.0.1', port), Handler)


def main():
    parser = argparse.ArgumentParser(description='本地 XML 战斗配置工具（Python 3 + Mono）')
    parser.add_argument('--port', type=int, default=18765)
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix='battle-content-editor-') as folder:
        print('正在编译现有 C# 规则程序集……', flush=True)
        compiler = Compiler(build(Path(folder)))
        with make_server(compiler, args.port) as server:
            print('配置工具：http://127.0.0.1:%d' % server.server_port, flush=True)
            print('仅本机访问；Ctrl+C 退出。草稿保存在浏览器中，XML 导出到下载目录。', flush=True)
            try:
                server.serve_forever()
            except KeyboardInterrupt:
                print('\n已停止配置工具。')


if __name__ == '__main__':
    main()
