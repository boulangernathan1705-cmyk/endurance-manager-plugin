# Ce que SimHub permet (et non)

Relevé du 20/11/2026. Le mur et l'overlay relais vont sur un **écran secondaire**, pas par-dessus le jeu.

## L'écran du plugin

- Un `UserControl` WPF (`IWPFSettingsV2`) dans la fenêtre de SimHub : liberté totale en WPF, et le plugin peut ouvrir
  ses propres fenêtres (guide, deuxième écran).
- Impossible de changer le menu de gauche ou la barre du haut de SimHub.
- Les contrôles stylés de SimHub (`SHSection`, `ControlsEditor`…) ne sont pas documentés : le wiki du SDK prévient
  qu'ils peuvent casser à une mise à jour. On utilise du WPF simple + `Theme.cs`.
- Exemples du SDK sur un PC avec SimHub : `C:\Program Files (x86)\SimHub\PluginSdk` (`User.PluginSdkDemo`).

## Dash Studio

- Mise en page en pixels absolus, tables à lignes fixes, graphiques limités, images par seconde limitées en licence
  gratuite (non confirmé), `WebPageItem` peu fiable (forum). D'où les écarts entre maquettes HTML et dashs.
- Les overlays demandent LMU en fenêtre sans bordure ; les dashs sont faits pour un 2ᵉ écran, un téléphone ou une
  tablette.

## Piste proposée (en attente de l'avis de Nathan)

Garder le plugin SimHub (données, boutons, black box, réglages, propriétés Dash Studio) et lui faire servir une **page
web locale** pour le mur et l'overlay relais (2ᵉ écran plein écran, tablette sur le réseau local ; une autorisation du
pare-feu Windows la première fois). La maquette du mur validée est déjà en HTML (`maquettes/mur-simhub/`).

Maquettes de l'écran du plugin (accueil avec tous les modules au lieu de deux niveaux d'onglets, pages de module avec
retour, « où afficher le mur », guide dans sa propre fenêtre) : https://claude.ai/artifact/VSzF2ZfcaDRuQYGd5baCUH
