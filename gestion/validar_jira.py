# -*- coding: utf-8 -*-
"""Valida el contenido creado en Jira (proyecto PHO)."""
import os
import sys
from urllib.parse import quote

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jira_lib import Jira  # noqa: E402

j = Jira("phidalgo@unsa.edu.pe",
         open(r"C:\Users\paulo_xxg0vy8\Downloads\jira.txt").read().strip())

r = j.get("/rest/api/3/search/jql?jql=" + quote("project=PHO ORDER BY key")
          + "&maxResults=100&fields=summary,status,priority,parent,fixVersions,"
            "customfield_10016,sprint")
issues = r.get("issues", r.get("values", []))
print(f"TOTAL ISSUES: {len(issues)}\n")
stats = {}
for i in issues:
    f = i["fields"]
    st = f["status"]["name"]
    stats[st] = stats.get(st, 0) + 1
    par = (f.get("parent") or {}).get("key", "-")
    ver = ",".join(v["name"] for v in f.get("fixVersions", [])) or "-"
    pts = f.get("customfield_10016")
    print("%-7s | %-12s | pts=%-4s | %-8s | epic=%-6s | %-16s | %s"
          % (i["key"], st, pts, f["priority"]["name"], par, ver,
             f["summary"][:45].encode("ascii", "replace").decode()))

print("\nESTADOS:", stats)

boards = j.get("/rest/agile/1.0/board?projectKeyOrId=PHO")["values"]
bid = boards[0]["id"]
sps = j.get(f"/rest/agile/1.0/board/{bid}/sprint?state=future,active,closed")["values"]
print(f"\nSPRINTS en tablero {bid}:")
for s in sorted(sps, key=lambda x: x["id"]):
    n = len(j.get(f"/rest/agile/1.0/sprint/{s['id']}/issue?maxResults=100")["issues"])
    print(f"  [{s['state']:7}] {s['name']:35} issues={n} "
          f"{s.get('startDate','')[:10]} -> {s.get('endDate','')[:10]}")

# comentarios de muestra
for k in ("PHO-14", "PHO-15", "PHO-28"):
    cs = j.get(f"/rest/api/3/issue/{k}/comment?maxResults=10").get("comments", [])
    print(f"\nComentarios en {k}: {len(cs)}")
