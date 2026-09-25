# -*- coding: utf-8 -*-
"""Cliente minimo para la API REST de Jira Cloud (stdlib, sin dependencias)."""
import base64
import json
import urllib.request
import urllib.error

BASE = "https://paulo-lab.atlassian.net"


class Jira:
    def __init__(self, email, token, base=BASE):
        self.base = base.rstrip("/")
        cred = base64.b64encode(f"{email}:{token}".encode()).decode()
        self.headers = {
            "Authorization": f"Basic {cred}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        }

    def req(self, method, path, payload=None):
        url = self.base + path if path.startswith("/") else path
        data = json.dumps(payload).encode("utf-8") if payload is not None else None
        r = urllib.request.Request(url, data=data, headers=self.headers, method=method)
        try:
            with urllib.request.urlopen(r, timeout=60) as resp:
                body = resp.read().decode("utf-8")
                return json.loads(body) if body else {}
        except urllib.error.HTTPError as e:
            body = e.read().decode("utf-8", "replace")
            raise RuntimeError(f"{method} {path} -> {e.code}: {body[:800]}")

    def get(self, p):
        return self.req("GET", p)

    def post(self, p, payload):
        return self.req("POST", p, payload)

    def put(self, p, payload):
        return self.req("PUT", p, payload)


def p(text):
    """Parrafo ADF."""
    return {"type": "paragraph", "content": [{"type": "text", "text": text}]}


def h(level, text):
    return {"type": "heading", "attrs": {"level": level},
            "content": [{"type": "text", "text": text}]}


def bullets(items):
    return {"type": "bulletList",
            "content": [{"type": "listItem", "content": [p(i)]} for i in items]}


def adf(*blocks):
    return {"type": "doc", "version": 1, "content": list(blocks)}
