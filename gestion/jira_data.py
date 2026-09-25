# -*- coding: utf-8 -*-
"""Datos del proyecto PHOBIA: Ecos en la Oscuridad (metodologia SUM) - parte 1."""

PROJECT_KEY = "PHO"
PROJECT_NAME = "PHOBIA - Ecos en la Oscuridad (Proyecto DSJ)"

PROJECT_DESC = (
    "Videojuego de terror en primera persona tipo Hidden Object desarrollado con la "
    "metodologia agil SUM (Laboratorio DSJ - UNSA).\n\n"
    "FICHA DE CONCEPTO: El jugador explora una instalacion abandonada generada "
    "proceduralmente, gestionando su nivel de ansiedad mientras busca evidencias y "
    "objetos clave ocultos en la oscuridad antes de que se agote el tiempo.\n"
    "Genero: Terror / Hidden Object 3D primera persona | Publico: 13+ | "
    "Plataforma: PC Windows | Motor: Unity 6 (URP).\n"
    "Core loop: Explorar -> gestionar ansiedad -> encender luces/interactuar -> "
    "encontrar evidencias -> escapar a tiempo.\n\n"
    "ROLES SUM: Cliente = docente del curso | Productor interno (Scrum Master) = "
    "Paulo Hidalgo | Equipo de desarrollo = Paulo Hidalgo (programacion y diseno de "
    "niveles) y Gabriel Bernedo (arte, audio y pruebas) | Verificador beta = 5 "
    "usuarios externos.\n\n"
    "DoD: codigo integrado sin errores de compilacion; funcionalidad probada segun "
    "criterios de aceptacion; sin defectos criticos conocidos; assets con nombres y "
    "carpetas consistentes; commit identificado y tablero actualizado; build "
    "ejecutable disponible cuando corresponda."
)

EPICS = [
    ("FASE SUM: Concepto", "#155E75"),
    ("FASE SUM: Planificacion", "#6D4C41"),
    ("FASE SUM: Elaboracion Iterativa", "#1B5E20"),
    ("FASE SUM: Beta", "#B26A00"),
    ("FASE SUM: Cierre", "#4A148C"),
    ("Gestion de Riesgos", "#B71C1C"),
]

# (nombre, objetivo, inicio, fin, estado)
SPRINTS = [
    ("ITER-1 Nucleo jugable", "Build jugable minima: explorar, encender luz, encontrar botiquin y ganar/perder.",
     "2026-08-10", "2026-08-24", "closed"),
    ("ITER-2 Atmosfera y sistemas", "Tension de terror: ansiedad, luces dinamicas, menus y generacion procedural.",
     "2026-08-24", "2026-09-07", "closed"),
    ("ITER-3 Contenido y progresion", "Feature complete: fobias, entidad perseguidora, progresion, evidencias y puntaje.",
     "2026-09-07", "2026-09-28", "active"),
    ("SPRINT BETA", "Validar que el juego funciona y la experiencia es aceptable con usuarios externos.",
     "2026-09-28", "2026-10-05", "future"),
    ("SPRINT CIERRE", "Build final, manual retrospectiva y acta de cierre.",
     "2026-10-05", "2026-10-12", "future"),
]

VERSIONS = [
    ("v0.1.0-alpha-it1", True, "2026-08-24", "Alpha: nucleo jugable (ITER-1)."),
    ("v0.2.0-alpha-it2", True, "2026-09-07", "Alpha: atmosfera y sistemas (ITER-2)."),
    ("v0.9.0-beta", False, "2026-10-05", "Version beta para pruebas con usuarios."),
    ("v1.0.0-release", False, "2026-10-12", "Entrega final del proyecto."),
]

# hitos documentados en la historia P-03 (Planificacion)
MILESTONES = [
    "H1 (10/08): Concepto aprobado por el cliente",
    "H2 (24/08): Alpha - nucleo jugable (fin ITER-1)",
    "H3 (07/09): Alpha - atmosfera y sistemas (fin ITER-2)",
    "H4 (28/09): Feature complete (fin ITER-3)",
    "H5 (05/10): Beta validada con usuarios externos",
    "H6 (12/10): Release final y acta de cierre",
]
