"""Clean FAH release: modules + self-contained publish + VC++ runtime for ONNX Runtime + licenses -> folder and zip.
Usage: python -I empacotar.py <empty out folder> [dotnet.exe]. Result: <out>\\FAH and <out>\\FAH-win-x64.zip"""
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
MODULOS = ["Efeitos", "Equalizador", "Espacial", "Microfone"]
VC = ["msvcp140.dll", "msvcp140_1.dll", "vcruntime140.dll", "vcruntime140_1.dll"]  # onnxruntime.dll imports these; not on a formatted PC


def rodar(dotnet, *args):
    subprocess.run([dotnet, *args], cwd=RAIZ, check=True)


def main(saida, dotnet="dotnet"):
    saida = Path(saida).resolve()
    if saida.exists() and any(saida.iterdir()):
        sys.exit(f"pasta nao esta vazia: {saida}")
    app = saida / "FAH"

    for m in MODULOS:
        rodar(dotnet, "build", f"fonte/Modulos/{m}/{m}.csproj", "-c", "Release", "-nologo", "-v", "q")
    rodar(dotnet, "publish", "fonte/FAH/FAH.csproj", "-c", "Release", "-r", "win-x64", "--self-contained", "true",
          "-o", str(app), "-nologo", "-v", "q")

    modulos = app / "modulos"
    shutil.copytree(RAIZ / "fonte/FAH/bin/Release/net10.0-windows/modulos", modulos, dirs_exist_ok=True)
    sistema = Path(os.environ["WINDIR"]) / "System32"
    for dll in VC:
        shutil.copy2(sistema / dll, modulos / dll)

    for arq in app.rglob("*"):
        if arq.suffix.lower() in (".pdb", ".xml"):
            arq.unlink()
    for pasta in ("config", "dados", "webview", "relatorios"):
        if (app / pasta).exists():
            shutil.rmtree(app / pasta)
    shutil.copy2(RAIZ / "LICENSE", app / "LICENSE.txt")
    shutil.copy2(RAIZ / "THIRD-PARTY-NOTICES.txt", app / "THIRD-PARTY-NOTICES.txt")
    pacote = Path.home() / ".nuget/packages/microsoft.ml.onnxruntime/1.31.1/ThirdPartyNotices.txt"
    if pacote.exists():
        shutil.copy2(pacote, modulos / "onnxruntime-ThirdPartyNotices.txt")

    faltando = [n for n in ["FAH.exe", "modulos/FAH.Espacial.dll", "modulos/FAH.Microfone.dll", "modulos/FAH.Equalizador.dll",
                            "modulos/FAH.Efeitos.dll", "modulos/onnxruntime.dll", "ui/index.html", "ui/dados/autoeq.js"]
                if not (app / n).exists()]
    if faltando:
        sys.exit(f"faltando no pacote: {faltando}")

    with zipfile.ZipFile(saida / "FAH-win-x64.zip", "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for arq in sorted(app.rglob("*")):
            if arq.is_file():
                z.write(arq, arq.relative_to(saida).as_posix())
    print(saida / "FAH-win-x64.zip")


if __name__ == "__main__":
    main(*sys.argv[1:3])
