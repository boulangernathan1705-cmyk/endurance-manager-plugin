# Mur des stands

Deux murs, mêmes règles de calcul :

| | Mur des stands (Pour moi) | Mur des stands équipages (Avec mon équipe) |
| --- | --- | --- |
| Où | Dash « Endurance Manager · Mur » dans SimHub (2560 × 1440, 2ᵉ écran) | Page « Mur des stands » du site (`mur.html`), PC et téléphone |
| Données | Ce PC seul : `LocalRoom.cs` refait le salon du site en C# | Le plugin envoie au site (`LiveShare.cs`), un salon Cloudflare Durable Object par équipe |
| Fichiers | `Wall.cs`, `WallDash.cs`, `dash/` (maquette → dash), `Fonts.cs`, `LocalRoom.cs` | `LiveShare.cs` ; côté site `server/live.mjs`, `shared/live.mjs`, `front/pit-wall.mjs` |

Le rejeu vérifie que le mur solo est identique au mur passé par le salon du site (voir [tests-et-ci.md](tests-et-ci.md)).

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

## Le dash = la maquette (depuis la 2.7.0)

- `src/dash/mur.html` est la maquette validée, avec des attributs `data-*` qui la relient aux propriétés
  `EnduranceManagerPlugin.Mur.*` (légende en haut du fichier).
- `node src/dash/build.mjs` (Playwright + Chromium, fichiers du site dans `site/` ou `EM_SITE`) lit la page et écrit
  `src/dash/mur.json` (éléments Dash Studio, incorporés à la DLL, `WallDash.cs`). Le résultat est commité : le refaire
  après chaque changement de `mur.html`.
- `node src/dash/build.mjs valeurs.json sortie.png` dessine le dash avec des valeurs (par exemple
  `*.mur-milieu.json` du rejeu).
- `Wall.Make` remplit une valeur par propriété ; polices du site installées pour l'utilisateur (`Fonts.cs`).
- Historique : un premier dash dessiné en C# avait été refusé (« pas du tout la maquette ») ; cette version s'était
  perdue entre deux PR puis a été adaptée au mur solo en 2.7.0.
- Pas encore vérifié dans le vrai Dash Studio (liaisons de position, `blink`, polices).

## Données lues (rappel)

Pour chaque voiture : gomme par roue (mémoire LMU, roue de télémétrie à t+848+i×260 : `OptimalTemp` @236,
`CompoundIndex` @240, `CompoundType` @241), état des stands (API `watch/standings` : `pitState`, `pitting`,
`pitstops`). Chrono d'arrêt, tours de sortie, tour optimal/prévu, chocs, blocages : calculés par le plugin
(`Computed.cs`). Lectures fausses retirées en 2.5 : « SoC » @772, écarts de place @788/792, estimation du tour @472.
