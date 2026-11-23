# Compiler et tester sans SimHub

## Compiler

- Avec SimHub installé (Windows) : `dotnet build src/EnduranceManager.SimHub.csproj -c Release` prend les vraies DLL de
  SimHub (`-p:SimHubDir="D:\SimHub"` s'il est ailleurs).
- Sans SimHub (GitHub, cloud, Linux) : `-p:UseSimHubFacade=true`, ou automatique si SimHub est absent. La façade
  `reference/SimHub.Plugins` et `reference/GameReaderCommon` a les mêmes signatures, sans code ; elle n'est jamais
  copiée chez les pilotes. Newtonsoft.Json doit rester en 13.0.x comme celui de SimHub.
- .NET SDK 8. Sur Linux : `apt install dotnet-sdk-8.0`.

## ApiCheck

`dotnet run --project reference/ApiCheck -c Release -- src/bin/Release/EnduranceManager.SimHub.dll` liste chaque membre
de SimHub appelé par la DLL qui n'est pas dans `reference/simhub-api.txt` (vérifiés sur une DLL compilée avec le vrai
SimHub). `--ecrire` met la liste à jour depuis une DLL compilée sur un PC avec SimHub.

Encore « à vérifier » au 22/11/2026 : `IControlMapperInterface`, `GetControlMapperInterface`, `StartStopRole`
(module Réglages voiture en course).

## Enregistreur et rejeu

- Enregistreur (`Tape.cs`) : interrupteur dans l'onglet Général du plugin. Tout ce que le plugin lit pendant que le
  pilote roule (mémoire LMU 10 fois par seconde, réponses de l'API locale) va dans
  `Documents\Endurance Manager\Enregistrements\*.emrec`. Rien n'est envoyé.
- Mettre un `.emrec` dans `enregistrements/` de ce dépôt : la CI le rejoue à chaque PR.
- `tests/Replay` :
  - `dotnet run --project tests/Replay -c Release` : rejoue les séances de `tests/Replay/fixtures` (deux vraies séances
    de la base du site : Le Mans Peugeot 9X8, 39 tours sec ; Paul Ricard Oreca 07, 29 tours pluie). La mémoire de LMU
    est refaite à 10 Hz (`Synth.cs`), le code du plugin tourne dessus et ~1 350 vérifications sont faites, jusqu'au mur.
  - `-- rejouer <fichier.emrec>` : rejoue un enregistrement, chiffres du dash dans `out/`.
  - Le salon du mur passe par le vrai code du site : `tests/room-bridge.mjs` importe `shared/live.mjs` du site (dossier
    `site/` à la racine, ou variable `EM_SITE`). Node 22.
- Les sorties (`*.mur.json`, `*.dash.json`, `*.sessions.json`) sont jointes à chaque exécution de la CI.

## Photos de l'écran

`dotnet run --project tests/Preview -c Release -- <dossier>` (Windows) photographie le guide et les onglets du plugin.
Sur une PR, la CI les range dans `maquettes/apercus-plugin/` sur la branche (commit `[skip ci]`).

`tests/dash-render.mjs` dessine un dash SimHub en HTML/PNG à partir de ses éléments et de valeurs (Playwright).

## La CI (`.github/workflows/plugin.yml`, Windows)

Compilation (façade) → ApiCheck → rejeu .NET Framework 4.8 → rejeu des enregistrements → photos → zip joint
(*Artifacts*, 30 jours ; sur une PR, aussi en pré-version `pr-N` dans Releases, remplacée à chaque envoi et retirée à la
fermeture de la PR). Sur `main` : Release GitHub si la version est nouvelle.

## Ce que seul Nathan peut tester

Le vrai SimHub et LMU : boutons, Control Mapper, Dash Studio, overlays. Dire toujours si un test est un rejeu, un
aperçu ou SimHub réel.
