# Publier une version

1. Dans la PR : monter `<Version>` dans `src/EnduranceManager.SimHub.csproj` (x.y.z, jamais réutiliser un numéro).
2. Nathan fusionne la PR dans `main`. La CI publie la Release `vx.y.z` avec `plugin-endurance-manager-x.y.z.zip`
   (DLL, `Installer le plugin.cmd`, `Convertir un dash NeoRed.ps1`).
3. Pour que les plugins installés (0.x) se mettent à jour : dans le dépôt du site, une PR vers `dev` qui met la DLL de
   la Release, `plugin.json` (`{"version":"x.y.z"}`) et `Installer le plugin.cmd` dans `connectors/simhub-plugin/v0/`,
   servis dans `/downloads/simhub/v0/` (voir [lien-avec-le-site.md](lien-avec-le-site.md) ; la première fois, ajouter
   ce dossier à `scripts/build.mjs`). Ne jamais toucher `connectors/simhub-plugin/release/` : c'est la 2.2.0 des
   anciens plugins. Le site publie la mise à jour à son
   déploiement (dev d'abord, puis production avec la release du site). Ne jamais monter `plugin.json` sans la DLL
   correspondante (boucle de mise à jour).

Le zip de la Release s'installe aussi tout seul : son installeur prend la DLL posée à côté de lui.

Chaque version affiche son identité « x.y.z (commit) » dans l'onglet Général.
