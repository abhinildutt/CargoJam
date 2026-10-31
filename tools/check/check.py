#!/usr/bin/env python3
"""Compile-check and unit-test Cargo Jam (Core, Meta, Game) without opening the Unity Editor.

Uses the .NET SDK bundled with Unity plus the reference lists Unity generated for the main
checkout's IDE projects, but globs every source file so new files are picked up. Works from any
git worktree of the repo.

    python3 -I tools/check/check.py            # compile Core + Game (editor and iOS player defines), run Core tests
    python3 -I tools/check/check.py --no-tests
    python3 -I tools/check/check.py --filter Solver

Needs the main checkout's Unity project to have been opened once (for Library/ScriptAssemblies
and the generated CargoJam.*.csproj files).
"""
import argparse
import os
import re
import subprocess
import sys

UNITY = "/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents"
DOTNET = UNITY + "/Resources/Scripting/DotNetSdk/dotnet"
NUNIT = UNITY + "/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net472/unity-custom/nunit.framework.dll"
EDITOR_DEFINES = {"UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_OSX", "UNITY_INCLUDE_TESTS"}

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
PROJECT = os.path.join(REPO, "CargoJam")
OUT = os.path.join(REPO, "tools", ".out")


def main_project():
    """The main checkout's Unity project. Worktrees share its .git dir, so ask git where that is."""
    r = subprocess.run(["git", "rev-parse", "--path-format=absolute", "--git-common-dir"],
                       cwd=HERE, capture_output=True, text=True)
    if r.returncode != 0:
        return PROJECT
    return os.path.join(os.path.dirname(r.stdout.strip()), "CargoJam")


MAIN_PROJECT = main_project()


def template(assembly):
    path = os.path.join(MAIN_PROJECT, f"CargoJam.{assembly}.csproj")
    if not os.path.exists(path):
        sys.exit(f"missing {path}: open the main project in Unity once so it generates IDE projects")
    with open(path, encoding="utf-8-sig") as f:
        return f.read()


def extra_script_assemblies(text):
    """Runtime package assemblies the template doesn't reference yet (e.g. a newly added asmdef reference)."""
    lib = os.path.join(MAIN_PROJECT, "Library", "ScriptAssemblies")
    have = set(re.findall(r'<Reference Include="([^"]+)"', text))
    refs = []
    for name in sorted(os.listdir(lib)):
        if not name.endswith(".dll") or name.startswith("CargoJam"):
            continue
        stem = name[:-4]
        if stem in have or "Editor" in stem or "Tests" in stem or "CodeGen" in stem:
            continue
        refs.append(f'    <Reference Include="{stem}"><HintPath>{os.path.join(lib, name)}</HintPath></Reference>\n')
    return "".join(refs)


def make_project(assembly, player=False):
    # Meta is engine-free like Core, so it borrows Core's template until Unity generates its own.
    if assembly == "Meta" and not os.path.exists(os.path.join(MAIN_PROJECT, "CargoJam.Meta.csproj")):
        text = template("Core").replace("CargoJam.Core", "CargoJam.Meta")
    else:
        text = template(assembly)
    if assembly == "Game" and "CargoJam.Meta.csproj" not in text:
        text = text.replace('<ProjectReference Include="CargoJam.Core.csproj" />',
                            '<ProjectReference Include="CargoJam.Core.csproj" />\n    <ProjectReference Include="CargoJam.Meta.csproj" />')
    text = text.replace("<HintPath>Library/", f"<HintPath>{MAIN_PROJECT}/Library/")
    text = re.sub(r'\s*<Compile Include="[^"]*" />', "", text)
    src = os.path.join(PROJECT, "Assets", "Scripts", assembly)
    glob = f'  <ItemGroup>\n    <Compile Include="{src}/**/*.cs" />\n{extra_script_assemblies(text)}  </ItemGroup>\n'
    text = text.replace("</Project>", glob + "</Project>")
    suffix = ".Player" if player else ""
    if player:
        def strip(m):
            keep = [d for d in m.group(1).split(";") if d not in EDITOR_DEFINES]
            return "<DefineConstants>" + ";".join(keep) + "</DefineConstants>"
        text = re.sub(r"<DefineConstants>([^<]*)</DefineConstants>", strip, text)
        text = text.replace('<ProjectReference Include="CargoJam.Core.csproj" />',
                            '<ProjectReference Include="CargoJam.Core.csproj" />')
    text = text.replace("<OutputPath>Temp/bin/Debug/</OutputPath>", f"<OutputPath>bin{suffix}/</OutputPath>")
    text = text.replace("<BaseIntermediateOutputPath>Temp/obj/$(MSBuildProjectName)</BaseIntermediateOutputPath>",
                        f"<BaseIntermediateOutputPath>obj{suffix}/$(MSBuildProjectName)</BaseIntermediateOutputPath>")
    name = f"CargoJam.{assembly}{suffix}.csproj"
    with open(os.path.join(OUT, name), "w") as f:
        f.write(text)
    return name


TEST_PROJECT = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <EnableDefaultItems>false</EnableDefaultItems>
    <NoWarn>CS0618;CS8632;NU1701</NoWarn>
    <AssemblyName>CoreTests</AssemblyName>
    <OutputPath>bin/CoreTests/</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="{core}/**/*.cs" />
    <Compile Include="{meta}/**/*.cs" />
    <Compile Include="{tests}/**/*.cs" />
    <Compile Include="{here}/TestRunner.cs" />
    <Compile Include="{here}/UnityShim.cs" />
    <Reference Include="nunit.framework"><HintPath>{nunit}</HintPath></Reference>
  </ItemGroup>
</Project>
"""


def build(project, label):
    print(f"== build {label}")
    r = subprocess.run([DOTNET, "build", project, "-nologo", "-v:q", "-clp:NoSummary;ErrorsOnly"],
                       cwd=OUT, capture_output=True, text=True)
    lines = [l for l in (r.stdout + r.stderr).splitlines() if "error" in l or "warning CS" in l]
    seen = []
    for l in lines:
        l = l.replace(REPO + "/", "")
        l = re.sub(r"\s*\[[^\]]*\.csproj\]$", "", l)
        if l not in seen:
            seen.append(l)
    for l in seen[:60]:
        print("  " + l)
    ok = r.returncode == 0
    print(f"   {'ok' if ok else 'FAILED'}")
    return ok


def main():
    sys.stdout.reconfigure(line_buffering=True)
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-tests", action="store_true")
    ap.add_argument("--filter", default="")
    args = ap.parse_args()

    os.makedirs(OUT, exist_ok=True)
    os.environ.setdefault("DOTNET_CLI_TELEMETRY_OPTOUT", "1")

    make_project("Core")
    make_project("Meta")
    game = make_project("Game")
    player = make_project("Game", player=True)

    ok = build(game, "CargoJam.Core + CargoJam.Meta + CargoJam.Game (editor defines)")
    ok = build(player, "CargoJam.Game (iOS player defines)") and ok

    if not args.no_tests:
        with open(os.path.join(OUT, "CoreTests.csproj"), "w") as f:
            f.write(TEST_PROJECT.format(core=os.path.join(PROJECT, "Assets", "Scripts", "Core"),
                                        meta=os.path.join(PROJECT, "Assets", "Scripts", "Meta"),
                                        tests=os.path.join(PROJECT, "Assets", "Tests", "EditMode"),
                                        here=HERE, nunit=NUNIT))
        if build("CoreTests.csproj", "Core + Meta tests"):
            print("== run Core + Meta tests")
            dll = os.path.join(OUT, "bin", "CoreTests", "CoreTests.dll")
            r = subprocess.run([DOTNET, dll, args.filter], cwd=PROJECT)
            ok = ok and r.returncode == 0
        else:
            ok = False

    print("ALL OK" if ok else "CHECK FAILED")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
