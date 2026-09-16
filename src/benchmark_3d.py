"""Punto de entrada unificado para el modelo benchmark 3D.

Uso:
    python src\\benchmark_3d.py              # analisis + JSON
    python src\\benchmark_3d.py --solo-analizar  # sin exportar
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "benchmark_3d"))
from analizar import main

if __name__ == "__main__":
    main()
