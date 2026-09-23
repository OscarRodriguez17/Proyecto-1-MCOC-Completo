# -*- coding: utf-8 -*-
"""Semana 03 — Amplia el cache de secciones con las envolventes P-M de los
11 muros del Edificio B (aditivo). Uso: python scripts/semana03_muros_catalog.py
"""
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(AQUI, "..", "src"))
sys.path.insert(0, SRC)

from secciones import catalogo_muros  # noqa: E402

if __name__ == "__main__":
    catalogo_muros.main()