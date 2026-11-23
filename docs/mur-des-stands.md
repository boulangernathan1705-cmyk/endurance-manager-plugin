# Mur des stands

Deux murs, mêmes règles de calcul :

| | Mur des stands (Pour moi) | Mur des stands équipages (Avec mon équipe) |
| --- | --- | --- |
| Où | Fenêtre ouverte par le plugin (« Ouvrir le mur ») et tablette sur le wifi : page `src/mur/index.html` servie par `WallServer.cs`, port 8686 | Page « Mur des stands » du site (`mur.html`), PC et téléphone |
| Données | Ce PC seul : `LocalRoom.cs` refait le salon du site en C# | Le plugin envoie au site (`LiveShare.cs`), un salon Cloudflare Durable Object par équipe |
| Fichiers | `Wall.cs`, `WallServer.cs`, `src/mur/`, `LocalRoom.cs` (l'ancien dash : `WallDash.cs`, `WallProperties.cs`) | `LiveShare.cs` ; côté site `server/live.mjs`, `shared/live.mjs`, `front/pit-wall.mjs` |

Le rejeu vérifie que le mur solo est identique au mur passé par le salon du site (voir [tests-et-ci.md](tests-et-ci.md)).

## La fenêtre du mur (22/11/2026)

Plus de Dash Studio. Le plugin sert la page du mur (rangée dans la DLL) sur ce PC et sur le wifi :
- « Ouvrir le mur » lance Edge (sinon Chrome, sinon le navigateur par défaut) en mode application, sans barre, avec son
  propre profil (`%LOCALAPPDATA%\EnduranceManager\Combos\fenetre-mur`). La page renvoie sa place et sa taille
  (`/place`, seulement depuis ce PC) : la fenêtre se rouvre au même endroit. « Plein écran » ajoute `--start-fullscreen`.
- Tablette ou téléphone sur le même réseau : l'adresse affichée sur l'onglet (`http://<IP du PC>:8686/`). Windows
  demande une fois d'autoriser SimHub dans le pare-feu.
- La page lit `/vue` quatre fois par seconde (`{ vue: WallView, profil }`), rien quand elle est cachée. Le mur complet
  change une fois par seconde (Wall.cs), les entrées à chaque lecture du jeu.
- Disposition fixe 2560 × 1440 réduite à la taille de la fenêtre en gardant ses proportions, jamais réarrangée
  (Nathan). Maquette : `maquettes/mur-fenetre/`.
- Le rejeu vérifie que `/vue` répond avec le mur du rejeu et l'écrit dans `<séance>.vue.json`.

## Le dash SimHub validé (18/11/2026)

Maquette : `maquettes/mur-simhub/maquette-mur-simhub.html` (+ `.png`, `-alertes.png`), direction artistique du mur du
site (`pit-wall.css` : Chakra Petch pour les mots, JetBrains Mono pour les chiffres, fond `#0a0c10`, cartes `#131722`,
accent cyan `#00e5ff`, catégories hyper `#ff3366`, LMP2 `#2f8cff`, GT3 `#ffab00`).

Disposition de Nathan :
- Bande du haut complète : delta, secteurs, dernier/meilleur, position, temps et tours restants, météo.
- Classement de **notre catégorie seulement** (pos, n°, pilote·voiture, devant, leader, dernier, gomme des 4 pneus,
  tours du relais, colonne stands : AUX STANDS puis SORTIE n/3 pendant 3 tours).
- À côté : pneus (usure, température, frein, pression ; usure par tour moyenne 5 + dernier) et dégâts sur le dessin de
  la voiture vue de dessus.
- Colonne de droite : réglages (TC, TC cut, ABS, répartition, map moteur, régén, batterie).
- Bandeau rythme perso, puis piste à plat sur une ligne (notre voiture au centre, plus grosse).
- En bas : relatif (3 devant, 3 derrière), conso (carburant ET énergie : /tour, /5 tours, depuis le début du relais,
  ratio, tours restants), entrées (pédales, plus larges que hautes).
- Alertes : énergie/carburant < 2 tours, zone de dégâts en rouge, pneu < 30 % orange < 15 % rouge, réglage à changer en
  cyan. Seul le cadre clignote.

## Deux versions du dash

- `main` (2.6.0) : `WallDash.cs` dessine le dash en C# d'après la maquette, `WallProperties.cs` liste les propriétés.
- Ancienne branche `mur-dash-maquette` (supprimée le 23/11/2026, dernier commit `48531fc`, PR #2 fermée) : la refonte faite juste après le refus du premier dash (« c'est pas du tout la
  maquette ») : `dash/mur.html` = la maquette avec des attributs `data-*`, `node dash/build.mjs` la lit dans Chromium et
  écrit les éléments Dash Studio (`dash/mur.json`, incorporé à la DLL), polices du site installées pour l'utilisateur.
  Elle n'a jamais été fusionnée avec la 2.6.0 (conflits dans `Wall.cs`, `WallDash.cs`, `Plugin.cs`).
- Rien des deux n'est vérifié dans le vrai Dash Studio. Voir aussi la piste « page web locale » dans
  [limites-simhub.md](limites-simhub.md).

## Données lues (rappel)

Pour chaque voiture : gomme par roue (mémoire LMU, roue de télémétrie à t+848+i×260 : `OptimalTemp` @236,
`CompoundIndex` @240, `CompoundType` @241), état des stands (API `watch/standings` : `pitState`, `pitting`,
`pitstops`). Chrono d'arrêt, tours de sortie, tour optimal/prévu, chocs, blocages : calculés par le plugin
(`Computed.cs`). Lectures fausses retirées en 2.5 : « SoC » @772, écarts de place @788/792, estimation du tour @472.
