# -*- coding: utf-8 -*-
"""
setup_jira.py - Crea/actualiza el proyecto completo SUM en Jira Cloud para el
videojuego de terror "PHOBIA: Ecos en la Oscuridad". Es idempotente: se puede
re-ejecutar y completa solo lo que falte.

Uso:
    python setup_jira.py
Lee el token de API desde C:\\Users\\paulo_xxg0vy8\\Downloads\\jira.txt
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jira_lib import Jira, adf, p       # noqa: E402
from jira_data import (PROJECT_KEY, PROJECT_NAME, PROJECT_DESC, EPICS,  # noqa: E402
                       SPRINTS, VERSIONS)
from jira_issues_a import ISSUES_A       # noqa: E402
from jira_issues_b import ISSUES_B       # noqa: E402
from jira_issues_c import ISSUES_C       # noqa: E402
from jira_issues_d import ISSUES_D       # noqa: E402

EMAIL = "phidalgo@unsa.edu.pe"
TOKEN_FILE = r"C:\Users\paulo_xxg0vy8\Downloads\jira.txt"
ALL_ISSUES = ISSUES_A + ISSUES_B + ISSUES_C + ISSUES_D

DONE_NAMES = {"done", "hecho", "listo", "terminado", "completado", "complete"}
PROG_NAMES = {"in progress", "en curso", "en progreso", "en prueba"}


def load_token():
    if not os.path.exists(TOKEN_FILE):
        sys.exit(f"ERROR: no existe el archivo de token: {TOKEN_FILE}")
    tok = open(TOKEN_FILE, encoding="utf-8").read().strip().strip('"').strip("'")
    if not tok:
        sys.exit("ERROR: el archivo de token esta vacio")
    return tok


def search_issues(jira, jql, fields="summary,status"):
    from urllib.parse import quote
    q = quote(jql)
    try:
        r = jira.get(f"/rest/api/3/search/jql?jql={q}&maxResults=100&fields={fields}")
        return r.get("issues", r.get("values", []))
    except RuntimeError:
        r = jira.get(f"/rest/api/3/search?jql={q}&maxResults=100&fields={fields}")
        return r.get("issues", [])


def transition_to(jira, key, names):
    trans = jira.get(f"/rest/api/3/issue/{key}/transitions")["transitions"]
    for t in trans:
        if t["name"].strip().lower() in names or \
           t.get("to", {}).get("name", "").strip().lower() in names:
            jira.post(f"/rest/api/3/issue/{key}/transitions",
                      {"transition": {"id": t["id"]}})
            return t["name"]
    return None


def adf_text(node):
    if isinstance(node, dict):
        if node.get("type") == "text":
            return node.get("text", "")
        return " ".join(adf_text(c) for c in node.get("content", []))
    if isinstance(node, list):
        return " ".join(adf_text(c) for c in node)
    return ""


def main():
    jira = Jira(EMAIL, load_token())

    print("[1/8] Autenticando...")
    me = jira.get("/rest/api/3/myself")
    lead = me["accountId"]
    print(f"      OK: {me['displayName']} ({me['emailAddress']})")

    print("[2/8] Proyecto...")
    try:
        proj = jira.get(f"/rest/api/3/project/{PROJECT_KEY}")
        print(f"      Reutilizando proyecto existente {proj['key']}")
    except RuntimeError:
        proj = jira.post("/rest/api/3/project", {
            "key": PROJECT_KEY, "name": PROJECT_NAME,
            "projectTypeKey": "software",
            "projectTemplateKey": "com.pyxis.greenhopper.jira:gh-simplified-agility-scrum",
            "description": PROJECT_DESC,
            "leadAccountId": lead, "assigneeType": "PROJECT_LEAD"})
        print(f"      Creado: {proj['key']}")
    pid = proj["id"]

    print("[3/8] Campo de puntos de historia...")
    pts_field = None
    for f in jira.get("/rest/api/3/field"):
        if f.get("name") in ("Story point estimate", "Story Points"):
            pts_field = f["id"]
            break
    print(f"      OK: {pts_field or 'no encontrado (se omiten puntos)'}")

    print("[4/8] Versiones...")
    existing_v = {v["name"]: v["id"]
                  for v in jira.get(f"/rest/api/3/project/{PROJECT_KEY}/versions")}
    ver_ids = []
    for name, released, rdate, desc in VERSIONS:
        if name in existing_v:
            ver_ids.append(existing_v[name])
            continue
        v = jira.post("/rest/api/3/version", {
            "name": name, "projectId": int(pid), "released": released,
            "releaseDate": rdate, "description": desc})
        ver_ids.append(v["id"])
    print(f"      OK: {len(ver_ids)} versiones")

    print("[5/8] Epics e historias...")
    epics = []
    existing = {i["fields"]["summary"]: i["key"]
                for i in search_issues(jira,
                                       f"project={PROJECT_KEY} AND issuetype=Epic")}
    for ename, _color in EPICS:
        if ename in existing:
            epics.append(existing[ename])
        else:
            ep = jira.post("/rest/api/3/issue", {"fields": {
                "project": {"key": PROJECT_KEY}, "issuetype": {"name": "Epic"},
                "summary": ename, "description": adf(p(f"Epica SUM: {ename}."))}})
            epics.append(ep["key"])
    for e in epics:
        print(f"      Epic {e}")

    all_j = search_issues(jira, f"project={PROJECT_KEY} ORDER BY key")
    key_by_hid = {}
    for i in all_j:
        s = i["fields"]["summary"]
        if s.startswith("[") and "]" in s:
            key_by_hid[s[1:s.index("]")]] = i["key"]

    for spec in ALL_ISSUES:
        if spec["id"] in key_by_hid:
            continue
        fields = {
            "project": {"key": PROJECT_KEY},
            "issuetype": {"name": spec["type"]},
            "summary": f"[{spec['id']}] {spec['summary']}",
            "description": adf(*spec["desc"]),
            "priority": {"name": spec["prio"]},
            "labels": spec["labels"],
            "assignee": {"accountId": lead},
        }
        if pts_field and spec.get("pts"):
            fields[pts_field] = spec["pts"]
        if spec.get("version") is not None:
            fields["fixVersions"] = [{"id": ver_ids[spec["version"]]}]
        try:
            fields["parent"] = {"key": epics[spec["epic"]]}
            r = jira.post("/rest/api/3/issue", {"fields": fields})
        except RuntimeError:
            fields.pop("parent", None)
            r = jira.post("/rest/api/3/issue", {"fields": fields})
        key_by_hid[spec["id"]] = r["key"]
        print(f"      Creado {r['key']} <- {spec['id']}")
    print(f"      Total issues mapeadas: {len(key_by_hid)}")

    print("[6/8] Sprints, asignacion y estados...")
    boards = jira.get(f"/rest/agile/1.0/board?projectKeyOrId={PROJECT_KEY}")["values"]
    bid = boards[0]["id"]
    sps = jira.get(f"/rest/agile/1.0/board/{bid}/sprint?state=future,active,closed")["values"]
    sp_by_name = {s["name"]: s for s in sps}

    for idx, (name, goal, start, end, state) in enumerate(SPRINTS):
        if name in sp_by_name:
            sp = sp_by_name[name]
        else:
            sp = jira.post("/rest/agile/1.0/sprint",
                           {"name": name, "originBoardId": bid, "goal": goal})
        sid, cur = sp["id"], sp.get("state", "future")
        issues = [key_by_hid[s["id"]] for s in ALL_ISSUES if s["sprint"] == idx
                  and s["id"] in key_by_hid]

        if state != "future" and cur == "future":
            jira.put(f"/rest/agile/1.0/sprint/{sid}", {
                "name": name, "goal": goal, "state": "active",
                "startDate": f"{start}T09:00:00.000Z",
                "endDate": f"{end}T18:00:00.000Z"})
            cur = "active"
        elif state == "future":
            jira.put(f"/rest/agile/1.0/sprint/{sid}", {
                "name": name, "goal": goal, "state": "future",
                "startDate": f"{start}T09:00:00.000Z",
                "endDate": f"{end}T18:00:00.000Z"})

        if issues and cur != "closed":
            jira.post(f"/rest/agile/1.0/sprint/{sid}/issue", {"issues": issues})
        for s in ALL_ISSUES:
            if s["sprint"] != idx or s["id"] not in key_by_hid:
                continue
            k = key_by_hid[s["id"]]
            if s["status"] == "done":
                transition_to(jira, k, DONE_NAMES)
            elif s["status"] == "progress":
                transition_to(jira, k, PROG_NAMES)
        if state == "closed" and cur == "active":
            jira.put(f"/rest/agile/1.0/sprint/{sid}", {
                "name": name, "goal": goal, "state": "closed",
                "startDate": f"{start}T09:00:00.000Z",
                "endDate": f"{end}T18:00:00.000Z",
                "completeDate": f"{end}T18:00:00.000Z"})
        print(f"      Sprint '{name}' -> {state} ({len(issues)} issues)")

    print("[7/8] Comentarios y enlaces de trazabilidad...")
    for s in ALL_ISSUES:
        k = key_by_hid.get(s["id"])
        if not k:
            continue
        existing_c = set()
        if s.get("comments"):
            for c in jira.get(f"/rest/api/3/issue/{k}/comment"
                              "?maxResults=50").get("comments", []):
                existing_c.add(adf_text(c.get("body", {})).strip())
        for c in s.get("comments", []):
            if c.strip() in existing_c:
                continue
            jira.post(f"/rest/api/3/issue/{k}/comment",
                      {"body": adf(p(c))})
        for b in s.get("blocked_by", []):
            try:
                jira.post("/rest/api/3/issueLink", {"type": {"name": "Blocks"},
                          "outwardIssue": {"key": key_by_hid[b]},
                          "inwardIssue": {"key": k}})
                print(f"      {b} bloquea a {s['id']}")
            except RuntimeError:
                pass

    print("[8/8] Resumen")
    url = "https://paulo-lab.atlassian.net"
    print(f"""
    Proyecto : {url}/browse/{PROJECT_KEY}
    Tablero  : {url}/jira/software/projects/{PROJECT_KEY}/boards/{bid}
    Backlog  : {url}/jira/software/projects/{PROJECT_KEY}/boards/{bid}/backlog
    Issues   : {len(key_by_hid)} | Epics: {len(epics)} | Versiones: {len(ver_ids)}
""")
    print("SETUP COMPLETO")


if __name__ == "__main__":
    main()
