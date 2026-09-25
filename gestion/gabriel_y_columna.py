# -*- coding: utf-8 -*-
"""1) Asigna historias a Gabriel Bernedo. 2) Intenta crear columna 'En prueba'."""
import json
import os
import sys
import urllib.error

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jira_lib import Jira, adf, p  # noqa: E402

j = Jira("phidalgo@unsa.edu.pe",
         open(r"C:\Users\paulo_xxg0vy8\Downloads\jira.txt").read().strip())

# ---------- 1. Buscar a Gabriel ----------
gid = None
for url in ("/rest/api/3/user/assignable/search?project=PHO&maxResults=50",
            "/rest/api/3/users/search?maxResults=50"):
    try:
        for u in j.get(url):
            if "BERNEDO" in u.get("displayName", "").upper():
                gid = u["accountId"]
        if gid:
            break
    except RuntimeError as e:
        print("busqueda fallo:", str(e)[:120])
if not gid:
    sys.exit("No se encontro la cuenta de Gabriel en el sitio.")
print("Gabriel accountId:", gid)

# ---------- 2. Asignar historias (subroles: arte, audio, pruebas) ----------
GABRIEL = ["PHO-22", "PHO-27", "PHO-28", "PHO-32", "PHO-34", "PHO-37"]
for k in GABRIEL:
    j.put(f"/rest/api/3/issue/{k}/assignee", {"accountId": gid})
    print(f"  {k} -> asignada a Gabriel")

j.post("/rest/api/3/issue/PHO-9/comment", {"body": adf(p(
    "Actualizacion: Gabriel Bernedo se unio al espacio Jira con acceso de "
    "miembro (ver Configuracion del espacio > Personas). Se le asignaron las "
    "historias de arte/audio/pruebas: HU-08, HU-13, HU-14, HU-18, HU-20 y HU-23."
))})
print("Comentario de roles actualizado en PHO-9")

# ---------- 3. Columna 'En prueba' ----------
print("\nProbando endpoints de columnas del tablero 2...")
for probe in ("/rest/agile/1.0/board/2/configuration",
              "/rest/gira/1.0/boards/2/columns"):
    try:
        r = j.get(probe)
        print(f"GET {probe} OK:", json.dumps(r)[:300])
    except RuntimeError as e:
        print(f"GET {probe} ->", str(e)[:150])

try:
    r = j.post("/rest/agile/1.0/board/2/column", {"name": "En prueba"})
    print("Columna creada:", json.dumps(r)[:200])
except RuntimeError as e:
    print("POST columna ->", str(e)[:200])
