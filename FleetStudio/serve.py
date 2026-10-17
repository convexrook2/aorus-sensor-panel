"""Read-only loopback host for the repository's standalone fleet viewer."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--port',type=int,default=8771)
args=parser.parse_args()

class Handler(SimpleHTTPRequestHandler):
    extensions_map={**SimpleHTTPRequestHandler.extensions_map,'.gz':'application/gzip'}
    def end_headers(self):
        self.send_header('Cache-Control','no-cache')
        super().end_headers()
    def list_directory(self,path):
        self.send_error(404,'Open index.html')
        return None

root=Path(__file__).resolve().parent
server=ThreadingHTTPServer(('127.0.0.1',args.port),partial(Handler,directory=str(root)))
print(f'Fleet Studio: http://127.0.0.1:{args.port}/?ship=Pella',flush=True)
try:
    server.serve_forever()
except KeyboardInterrupt:
    server.server_close()
