from http.server import HTTPServer, BaseHTTPRequestHandler
from transformers import pipeline
import cgi
import json

class AiServerHandler(BaseHTTPRequestHandler):
    classifier = None

    def do_POST(self):
        ctype, pdict = cgi.parse_header(self.headers.get('content-type'))

        if ctype != 'application/json':
            self.send_response(400)
            self.end_headers()
            return

        length = int(self.headers.get('content-length'))
        payload = self.rfile.read(length)
        message = json.loads(payload)

        content = message["content"]
        labels = message["labels"]

        if content is None or labels is None:
            print(f'received invalid payload: {payload}')
            self.send_response(400)
            self.end_headers()
            return

        prediction = self.classifier(content, labels)

        self.send_response(200)
        self.send_header('Content-type', 'application/json')
        self.end_headers()
        self.wfile.write(bytes(json.dumps({'labels': prediction['labels'], 'scores': prediction['scores']}), 'utf-8'))

class AiServer(HTTPServer):
    def __init__(self, server_address, port, h):
        h.classifier = pipeline('zero-shot-classification')
        print (f'server is started at {server_address}:{port}')
        super(AiServer, self).__init__((server_address, port), h)

AiServer('0.0.0.0', 80, AiServerHandler).serve_forever()