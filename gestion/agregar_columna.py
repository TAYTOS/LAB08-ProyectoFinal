# -*- coding: utf-8 -*-
"""Agrega la columna 'En prueba' al tablero PHO (simple board)."""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jira_lib import Jira  # noqa: E402

j = Jira("phidalgo@unsa.edu.pe",
         open(r"C:\Users\paulo_xxg0vy8\Downloads\jira.txt").read().strip())

cfg = j.get("/rest/agile/1.0/board/2/configuration")
cc = cfg.get("columnConfig")
print("columnConfig actual:")
print(json.dumps(cc, indent=1, ensure_ascii=False)[:1500])

print("\nIntento B: renombrar el estado 10007 a 'En prueba'...")
try:
    r = j.put("/rest/api/3/statuses/10007", {"name": "En prueba"})
    print("OK PUT statuses/10007:", json.dumps(r)[:200])
except RuntimeError as e:
    print("PUT statuses ->", str(e)[:250])
    try:
        r = j.post("/rest/api/3/statuses", {
            "scope": {"type": "PROJECT", "project": {"id": "10001"}},
            "statuses": [{"name": "En prueba", "statusCategory": "IN_PROGRESS"}]})
        print("Status creado:", json.dumps(r)[:250])
    except RuntimeError as e2:
        print("POST statuses(bulk) ->", str(e2)[:250])
