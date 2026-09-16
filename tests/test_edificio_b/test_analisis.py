# -*- coding: utf-8 -*-
"""Tests del modelo benchmark Edificio B: build, equilibrio, superposición,
Euler-Bernoulli."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "src"))

from edificio_b.construir import build_model
from edificio_b import verificar


def test_modelo_se_construye():
    M = build_model()
    assert len(M.coord) > 150
    assert M.eid > 250
    npil = sum(1 for t in M.tipo.values() if t == 'pilar')
    nmur = sum(1 for t in M.tipo.values() if t == 'muro')
    nvig = sum(1 for t in M.tipo.values() if t == 'viga')
    assert npil == 40      # 8 pilares x 5 tramos
    assert nmur == 60      # (8 paños principales + 4 del bloque) x 5 tramos
    assert nvig > 200


def test_equilibrio_global():
    assert verificar.verificar_equilibrio()


def test_superposicion():
    assert verificar.verificar_superposicion()


def test_euler_bernoulli():
    assert verificar.test_euler_bernoulli()