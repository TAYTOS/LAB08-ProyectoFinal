# -*- coding: utf-8 -*-
"""Exporta el backlog del proyecto PHO a CSV (Excel-friendly, UTF-8 BOM)."""
import csv
import os
import sys
from urllib.parse import quote

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jira_lib import Jira  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   "..", "backlog_PHO.csv")

j = Jira("phidalgo@unsa.edu.pe",
         open(r"C:\Users\paulo_xxg0vy8\Downloads\jira.txt").read().strip())

# Detectar el campo personalizado "Sprint" (customfield variable por instancia)
sprint_field = None
for f in j.get("/rest/api/3/field"):
    if f.get("name") == "Sprint" and f.get("custom"):
        sprint_field = f["id"]
        break

fields = ("summary,status,priority,parent,fixVersions,customfield_10016,"
          "assignee,issuetype,labels,created,resolutiondate")
if sprint_field:
    fields += "," + sprint_field
r = j.get("/rest/api/3/search/jql?jql=" + quote("project=PHO ORDER BY key")
          + f"&maxResults=100&fields={fields}")
issues = r.get("issues", r.get("values", []))

# Mapa id de sprint -> (nombre, estado)
boards = j.get("/rest/agile/1.0/board?projectKeyOrId=PHO")["values"]
bid = boards[0]["id"]
sprints = {s["id"]: (s["name"], s["state"])
           for s in j.get("/rest/agile/1.0/board/" + str(bid)
                          + "/sprint?state=future,active,closed")["values"]}

def sprint_info(f):
    if not sprint_field:
        return "", ""
    val = f.get(sprint_field)
    if not val:
        return "", ""
    if isinstance(val, list):
        val = val[-1]
    if isinstance(val, str):  # formato legacy "com.atlassian...name=...,state=..."
        import re
        name = re.search(r"name=([^,\]]+)", val)
        state = re.search(r"state=([^,\]]+)", val)
        return (name.group(1) if name else "", state.group(1) if state else "")
    if isinstance(val, dict):
        return val.get("name", ""), val.get("state", "")
    return "", ""

rows = []
for i in issues:
    f = i["fields"]
    par = f.get("parent") or {}
    sp_name, sp_state = sprint_info(f)
    asg = f.get("assignee") or {}
    rows.append({
        "Clave": i["key"],
        "Tipo": f["issuetype"]["name"],
        "Resumen": f["summary"],
        "Estado": f["status"]["name"],
        "Prioridad": f["priority"]["name"],
        "Puntos": f.get("customfield_10016") or "",
        "Epica": par.get("fields", {}).get("summary", "") if par else "",
        "Sprint": sp_name,
        "Estado Sprint": sp_state,
        "Version": "; ".join(v["name"] for v in f.get("fixVersions", [])),
        "Responsable": asg.get("displayName", ""),
        "Etiquetas": "; ".join(f.get("labels", [])),
        "Creado": (f.get("created") or "")[:10],
        "Resuelto": (f.get("resolutiondate") or "")[:10],
    })

with open(OUT, "w", newline="", encoding="utf-8-sig") as fh:
    w = csv.DictWriter(fh, fieldnames=list(rows[0].keys()), delimiter=";")
    w.writeheader()
    w.writerows(rows)

print(f"OK: {len(rows)} filas -> {os.path.abspath(OUT)}")
