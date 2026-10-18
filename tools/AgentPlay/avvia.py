#!/usr/bin/env python3
"""Starts the AgentPlay MCP server (the command of .mcp.json).

Builds AgentPlay quietly into a temp folder named by a hash of its sources
(%TEMP%/tindustry-agentplay-bin/<hash>), reuses it while the sources do not change, then runs the built dll with
stdio passed through. The repository's bin/obj are never used, so `dotnet build tIndustry.sln` keeps working while
the server runs. stdout is the MCP channel: every build line goes to stderr.

    python tools/AgentPlay/avvia.py             # build if needed, then serve over stdio
    python tools/AgentPlay/avvia.py --prepara   # build only (first start is slower: do it ahead)
"""
import hashlib
import os
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "tools" / "AgentPlay" / "AgentPlay.csproj"
OUT_ROOT = Path(tempfile.gettempdir()) / "tindustry-agentplay-bin"
CODE = {".cs", ".csproj", ".props", ".targets"}
OLD_BUILDS_DAYS = 7


def sources():
    """Every file the build reads: code of AgentPlay and Shared, the data it copies, the repo-wide build settings."""
    files = []
    for base in (ROOT / "tools" / "AgentPlay", ROOT / "src" / "TIndustry.Shared"):
        for path in base.rglob("*"):
            parts = path.relative_to(base).parts
            if path.is_file() and path.suffix.lower() in CODE and "bin" not in parts and "obj" not in parts:
                files.append(path)
    data = ROOT / "data"
    files += [data / "content.json", data / "campaign.json"]
    files += [p for p in (data / "maps").glob("*") if p.is_file()]
    files += [ROOT / n for n in ("global.json", "Directory.Build.props", "Directory.Build.targets", "NuGet.config")
              if (ROOT / n).is_file()]
    return sorted(files)


def source_hash():
    digest = hashlib.sha256()
    for path in sources():
        digest.update(path.relative_to(ROOT).as_posix().encode() + b"\0")
        # Line endings do not change the build: a CRLF and an LF checkout share one folder.
        digest.update(path.read_bytes().replace(b"\r\n", b"\n") + b"\0")
    return digest.hexdigest()[:16]


def built_dll(folder):
    return folder / "bin" / "AgentPlay" / "release" / "AgentPlay.dll"


def build(key, env):
    final = OUT_ROOT / key
    if built_dll(final).is_file():
        return built_dll(final)

    # Build next to the final folder, then rename it in one step: two launchers starting together never share a
    # half-built folder.
    work = OUT_ROOT / f"{key}.{os.getpid()}.tmp"
    shutil.rmtree(work, ignore_errors=True)
    print(f"AgentPlay: compilo in {final} (solo la prima volta per questi sorgenti)...", file=sys.stderr, flush=True)
    result = subprocess.run(
        ["dotnet", "build", str(PROJECT), "-c", "Release", "--artifacts-path", str(work), "-nologo", "-v", "q"],
        stdin=subprocess.DEVNULL, stdout=sys.stderr, stderr=sys.stderr, env=env)
    if result.returncode != 0 or not built_dll(work).is_file():
        shutil.rmtree(work, ignore_errors=True)
        sys.exit(f"AgentPlay: compilazione fallita (codice {result.returncode}).")
    try:
        os.rename(work, final)
    except OSError:
        shutil.rmtree(work, ignore_errors=True)
        if not built_dll(final).is_file():
            raise
    return built_dll(final)


def sweep(keep):
    """Builds of older sources, untouched for a week (a running server's folder is locked and stays)."""
    limit = time.time() - OLD_BUILDS_DAYS * 86400
    for folder in OUT_ROOT.iterdir():
        try:
            if folder.name != keep and folder.is_dir() and folder.stat().st_mtime < limit:
                shutil.rmtree(folder, ignore_errors=True)
        except OSError:
            pass  # another launcher renamed or removed it meanwhile


def main():
    if shutil.which("dotnet") is None:
        print("AgentPlay: comando 'dotnet' non trovato nel PATH (serve .NET 8 SDK).", file=sys.stderr)
        return 1
    env = dict(os.environ, DOTNET_NOLOGO="1", DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1")
    OUT_ROOT.mkdir(parents=True, exist_ok=True)
    key = source_hash()
    dll = build(key, env)
    os.utime(dll.parents[3])  # marks this build as in use for the sweep
    sweep(key)
    if "--prepara" in sys.argv[1:]:
        print(f"AgentPlay pronto: {dll}", file=sys.stderr)
        return 0
    # Not exec: on Windows os.exec* starts a new process and ends this one, which the MCP client would see as an exit.
    return subprocess.run(["dotnet", str(dll)], env=env).returncode


if __name__ == "__main__":
    sys.exit(main())
