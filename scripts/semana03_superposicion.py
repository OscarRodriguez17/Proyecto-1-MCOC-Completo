# -*- coding: utf-8 -*-
"""Semana 03 — Verificación §4 de superposición (G+EX, G+Q+EX) para A y B,
con corridas directas en OpenSees comparadas contra la suma de casos.
Uso: python scripts/semana03_superposicion.py
"""
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(AQUI, "..", "src"))
sys.path.insert(0, SRC)

from secciones import superposicion  # noqa: E402

if __name__ == "__main__":
    superposicion.main()