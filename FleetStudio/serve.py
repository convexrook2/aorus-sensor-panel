"""Read-only loopback host for the repository's standalone fleet viewer."""
import argparse
import logging
import os
import sys
import webbrowser
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from logging.handlers import RotatingFileHandler
from pathlib import Path
from urllib.parse import urlsplit

parser=argparse.ArgumentParser()
parser.add_argument('--port',type=int,default=8771)
parser.add_argument('--open',action='store_true',help='open the viewer in the default browser after binding')
parser.add_argument('--log-file',type=Path,help='write a rotating local server log at this path')
args=parser.parse_args()

log_file=args.log_file or Path(os.environ.get('LOCALAPPDATA',Path.home()))/'FleetStudio'/'server.log'
log_file.parent.mkdir(parents=True,exist_ok=True)
logger=logging.getLogger('fleet-studio')
logger.setLevel(logging.INFO)
formatter=logging.Formatter('%(asctime)s %(levelname)s pid=%(process)d %(message)s')
file_handler=RotatingFileHandler(log_file,maxBytes=1_000_000,backupCount=2,encoding='utf-8')
file_handler.setFormatter(formatter)
logger.addHandler(file_handler)
if sys.stderr:
    console_handler=logging.StreamHandler(sys.stderr)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)

class Server(ThreadingHTTPServer):
    def handle_error(self,request,client_address):
        logger.exception('Request failed for %s',client_address)

class Handler(SimpleHTTPRequestHandler):
    extensions_map={**SimpleHTTPRequestHandler.extensions_map,'.gz':'application/gzip'}
    def do_GET(self):
        # Keep previously shared authoring-viewer links usable at this host.
        url=urlsplit(self.path)
        if url.path.rstrip('/')=='/health':
            payload=b'Fleet Studio ready\n'
            self.send_response(200)
            self.send_header('Content-Type','text/plain; charset=utf-8')
            self.send_header('Content-Length',str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)
            return
        if url.path.rstrip('/') in ('/Fleet-Studio/Successor','/Fleet-Studio/Review','/fleet-preview'):
            self.send_response(307)
            self.send_header('Location','/'+('?' + url.query if url.query else ''))
            self.end_headers()
            return
        super().do_GET()
    def log_message(self,format,*args):
        logger.info('%s - %s',self.address_string(),format % args)
    def end_headers(self):
        self.send_header('Cache-Control','no-cache')
        super().end_headers()
    def list_directory(self,path):
        self.send_error(404,'Open index.html')
        return None

root=Path(__file__).resolve().parent
try:
    server=Server(('127.0.0.1',args.port),partial(Handler,directory=str(root)))
except OSError:
    logger.exception('Could not bind Fleet Studio to port %s',args.port)
    raise
url=f'http://127.0.0.1:{args.port}/'
logger.info('Fleet Studio serving %s from %s; python=%s; log=%s',url,root,sys.executable,log_file)
if sys.stdout:
    print(f'Fleet Studio: {url}',flush=True)
if args.open:
    webbrowser.open_new_tab(url)
try:
    server.serve_forever()
except KeyboardInterrupt:
    pass
finally:
    server.server_close()
    logger.info('Fleet Studio stopped')
