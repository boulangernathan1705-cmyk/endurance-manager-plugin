# Publier une version

1. Dans la PR : monter `<Version>` dans `src/EnduranceManager.SimHub.csproj` (x.y.z, jamais réutiliser un numéro).
2. Nathan fusionne la PR dans `main`. La CI publie la Release `vx.y.z` avec `plugin-endurance-manager-x.y.z.zip`
   (DLL, `Installer le plugin.cmd`, `Convertir un dash NeoRed.ps1`).
3. Pour que les plugins installés se mettent à jour : dans le dépôt du site, une PR vers `dev` qui remplace
   `connectors/simhub-plugin/release/EnduranceManager.SimHub.dll` par celle de la Release et met la version dans
   `connectors/simhub-plugin/release/plugin.json` (`{"version":"x.y.z"}`). Le site publie la mise à jour à son
   déploiement (dev d'abord, puis production avec la release du site). Ne jamais monter `plugin.json` sans la DLL
   correspondante (boucle de mise à jour).
4. Si `Installer le plugin.cmd` a changé, le copier aussi dans `connectors/simhub-plugin/` du site.

Le zip de la Release s'installe aussi tout seul : son installeur prend la DLL posée à côté de lui.

Chaque version affiche son identité « x.y.z (commit) » dans l'onglet Général.
