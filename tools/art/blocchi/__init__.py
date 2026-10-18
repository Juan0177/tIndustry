"""Sprite registry: one module per family (produzione, logistica, potenza, core, drone, oggetti).

Each module registers its sprites with the decorator:

    @sprite("miner-base")
    def miner_base():
        ...
        return im

The name is the PNG file name in godot/assets/ without ".png". Animation strips
end in "-anim"; their frames are square (side = strip height) unless the decorator
declares the frame width: @sprite("smelter-glow-anim", fotogramma=64).
Every module in this package is imported automatically, in name order.
"""
import importlib
import pkgutil

SPRITE = {}  # name -> function returning a PIL RGBA image
FOTOGRAMMA = {}  # "-anim" name -> frame width when it is not the strip height


def sprite(nome, fotogramma=None):
    def registra(fn):
        if nome in SPRITE and SPRITE[nome] is not fn:
            raise ValueError(f"sprite registrato due volte: {nome}")
        SPRITE[nome] = fn
        if fotogramma is not None:
            FOTOGRAMMA[nome] = fotogramma
        return fn
    return registra


for _modulo in sorted(m.name for m in pkgutil.iter_modules(__path__)):
    importlib.import_module(f"{__name__}.{_modulo}")
