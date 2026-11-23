# Publier une version

1. Dans la PR : monter `<Version>` dans `src/EnduranceManager.SimHub.csproj` (x.y.z, jamais réutiliser un numéro) et
   écrire `src/changements.json` pour cette version : une phrase par changement, `type` = `nouveau`, `mieux` ou
   `corrige`, `page` = la page de l'écran où ça se trouve (`home`, `relais`, `profils`, `bb`, `mur`, `memo`, `equipe`,
   `dev`, `guide`, `souci`, `maj`) ou rien. La CI refuse un numéro différent du csproj. Le pilote les lit dans la petite
   fenêtre de mise à jour (`UpdateWindow.cs`) avant et après la mise à jour.
2. Nathan fusionne la PR dans `main`. La CI publie la Release `vx.y.z` avec `plugin-endurance-manager-x.y.z.zip`
   (DLL, `Installer le plugin.cmd`, `Convertir un dash NeoRed.ps1`, `plugin.json`) et `plugin.json` à part (version,
   taille, empreinte SHA-256, changements).
   Si aucune exécution « push » n'apparaît dans Actions après la fusion (fusion faite par une appli), lancer
   Actions › Plugin › Run workflow sur `main` : la Release part de la même façon.
3. La même exécution de la CI ouvre la PR du site (`app-endurance-manager`, branche `plugin-x.y.z` vers `dev`) qui
   remplace `connectors/simhub-plugin/v0/` par la DLL, le `plugin.json` et l'installeur de la Release. Il faut une fois
   pour toutes le secret `SITE_TOKEN` dans ce dépôt (Settings › Secrets and variables › Actions) : un jeton GitHub
   « fine-grained » limité au dépôt du site, avec Contents et Pull requests en écriture. Sans lui, la CI avertit et
   rien ne part : faire alors cette PR à la main avec les mêmes fichiers.
4. Fusionner la PR du site dans `dev` (CI verte), puis elle part en production avec la release du site, après le
   « go prod » de Nathan. Alors seulement les plugins installés la proposent dans SimHub. Le site sert `v0/` deux fois
   (`scripts/build.mjs`) : `/downloads/simhub/v0/` pour les 0.x et la page Télémétrie Mémo, `/downloads/simhub/` (même
   DLL annoncée 2.99.0) pour que les anciens 2.x passent une fois à la 0.x. Le build du site refuse un `plugin.json`
   qui ne correspond pas à la DLL.

Le zip de la Release s'installe aussi tout seul : son installeur prend la DLL posée à côté de lui.

Chaque version affiche son identité « x.y.z (commit) » dans l'onglet Général.
