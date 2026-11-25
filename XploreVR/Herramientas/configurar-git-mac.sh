#!/bin/bash
# ===================================================================
#  Configura Git LFS y Smart Merge de Unity en ESTE repositorio.
#  Ejecutadlo UNA vez cada uno, en vuestro ordenador, después de clonar:
#     bash Herramientas/configurar-git-mac.sh
# ===================================================================
cd "$(dirname "$0")/.." || exit 1

echo "[1/2] Activando Git LFS..."
if ! git lfs install; then
  echo "  No se encontró git-lfs. Instaladlo con:  brew install git-lfs   (o GitHub Desktop, que lo trae)"
  exit 1
fi

echo "[2/2] Buscando UnityYAMLMerge (Smart Merge)..."
MERGE=$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/Tools/UnityYAMLMerge 2>/dev/null | tail -1)
if [ -z "$MERGE" ]; then
  echo "  No encuentro UnityYAMLMerge. Buscadlo dentro de Unity.app/Contents/Tools/ y editad este script."
  exit 1
fi
git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'$MERGE' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
echo "Listo. Usando: $MERGE"
