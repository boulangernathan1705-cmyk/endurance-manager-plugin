# Publier une version

1. Dans la PR : monter `<Version>` dans `src/EnduranceManager.SimHub.csproj` (x.y.z, jamais réutiliser un numéro) et
   écrire `src/changements.json` pour cette version : une phrase par changement, `type` = `nouveau`, `mieux` ou
   `corrige`, `page` = la page de l'écran où ça se trouve (`home`, `relais`, `profils`, `bb`, `mur`, `memo`, `equipe`,
   `dev`, `guide`, `souci`, `maj`) ou rien. La CI refuse un numéro différent du csproj. Le pilote les lit dans la petite
   fenêtre de mise à jour (`UpdateWindow.cs`) avant et après la mise à jour.
2. Nathan fusionne la PR dans `main`. La CI publie la Release `vx.y.z` avec `plugin-endurance-manager-x.y.z.zip`
   (DLL, `Installer le plugin.cmd`, `Convertir un dash NeoRed.ps1`, `plugin.json`) et `plugin.json` à part (version,
   taille, empreinte SHA-256, changements).
3. Pour que les plugins installés (0.x) se mettent à jour : dans le dépôt du site, une PR vers `dev` qui met la DLL de
   la Release, son `plugin.json` (tel quel : le plugin vérifie taille et empreinte) et `Installer le plugin.cmd` dans `connectors/simhub-plugin/v0/`,
   servis dans `/downloads/simhub/v0/` (voir [lien-avec-le-site.md](lien-avec-le-site.md) ; la première fois, ajouter
   ce dossier à `scripts/build.mjs`). Ne jamais toucher `connectors/simhub-plugin/release/` : c'est la 2.2.0 des
   anciens plugins. Le site publie la mise à jour à son
   déploiement (dev d'abord, puis production avec la release du site). Ne jamais monter `plugin.json` sans la DLL
   correspondante (boucle de mise à jour).

Le zip de la Release s'installe aussi tout seul : son installeur prend la DLL posée à côté de lui.

Chaque version affiche son identité « x.y.z (commit) » dans l'onglet Général.
