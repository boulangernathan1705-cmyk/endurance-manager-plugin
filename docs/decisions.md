# Décisions et idées refusées

Ce que Nathan a décidé, avec la date. Les idées refusées ne sont pas à reproposer.

## Manière de travailler

- 26/11/2026 : ChatGPT, Claude, GitHub Copilot ou toute autre IA autorisée utilisent une base de connaissances commune sur GitHub, sans communication automatisée entre elles. Nathan pilote le produit par demandes et itérations, sans coder ; les agents prennent en charge la réalisation technique. Voir [ai-workflow.md](ai-workflow.md).

- Nouvelle fonction ou nouvel écran : proposer et dessiner ensemble d'abord (efficace, joli, ergonomique), coder après
  accord. Petites corrections : directement.
- Toujours spécifique à LMU, jamais de règle iRacing.
- Un seul chantier à la fois sur le plugin.
- 22/11/2026 : le plugin vit dans son propre dépôt GitHub (celui-ci), pour travailler depuis un téléphone, avec ChatGPT
  ou seul, sans le PC de Nathan. `main` = ce que les pilotes installent, une branche par sujet, PR validée par Nathan.
- 23/11/2026 : on travaille **uniquement sur GitHub** (compilation par la CI, zip « Test PR #N »), jamais dans les
  dossiers du PC, même depuis le PC : une session avait repris une vieille copie locale.
- 22/11/2026 : numérotation repartie de 0.1.0 (« c'est un début qu'on va beaucoup travailler »).
- 22/11/2026 : le mur des stands ne sera **pas** un dash Dash Studio (trop dur à rendre fidèle). Il garde les mêmes
  informations, venues du plugin, affichées à la façon de LMU Drive (page ou fenêtre à lui). Proposition en cours.

## Produit

- 18/10/2026 : préparer les endurances (mémo d'équipe : énergie, pneus, arrêts), pas d'overlay de course type radar ou
  classement (l'équipe en a déjà). Un overlay SimHub pour les essais est voulu.
- Données brutes uniquement, aucun conseil. L'ancienne version (retirée du site en v1.51) a échoué parce qu'elle était
  trop compliquée et donnait des conseils.
- L'énergie virtuelle est le facteur limitant (pas les tours par plein) : montrer la longueur du relais → la conso à
  tenir. Énergie visée = 100 % / tours par relais.
- Temps et usure visés : w × ma moyenne + (1 − w) × moyenne de l'équipe, w = min(1, mes tours / (2 × tours par relais)).
  Tours > 107 % du meilleur exclus.
- Une couleur par pilote, la moyenne de l'équipe en blanc, le meilleur en violet.
- 18/11/2026 : un seul plugin qui fait tout, en modules activables (pas de plugins séparés).
- 19/11/2026 : le plugin doit marcher sans le site ; le lien au site est optionnel. Deux groupes de modules « Pour moi »
  et « Avec mon équipe ». Parcours guidé au premier lancement. D'abord que ça marche, la direction artistique du site
  en dernier.
- 19/11/2026 : FMT companion doit être dans le plugin, entier, comme un module qu'on coupe. Les commandes refaites
  doivent être identiques à la 0.5, jamais simplifiées.
- 19/11/2026 : l'écran des profils doit être simple (pas de tableau des rôles TC+/TC−).
- 22/11/2026 : profils = 1er appui sur le bouton, un popup montre ce qui change ; 2e appui dans les 5 s, appliqué ;
  appui de 2 s, mémorisé. Le popup se place où on veut (« Placer le popup »).
- 22/11/2026 : version renumérotée 0.1 (un début, beaucoup de travail à venir).
- Pas de barre de défilement horizontale (tables lisibles à 390 px de large). Textes d'explication au minimum, pas de
  « il y a X s ».

## Mur des stands

- Pour les coéquipiers qui regardent, et lisible par le pilote en roulant. Écran 2560 × 1440.
- 22/11/2026 : plus de Dash Studio (trop dur à rendre fidèle). Le mur est une page ouverte par le plugin dans une fenêtre
  qu'on place et redimensionne où on veut (15″ à 27″), et la même adresse sur tablette. Disposition fixe, réduite à la
  taille de la fenêtre comme un dash, jamais réarrangée. Mêmes données que SimHub.
- Alertes : seul le cadre concerné clignote, jamais de fenêtre ou de message qui masque le reste.
- Piste à plat sur une seule ligne (pas une ligne par catégorie), notre voiture plus grosse au milieu. Trois couleurs
  fixes par secteur : S1 citron vert `#c6ff00`, S2 blanc `#ffffff`, S3 menthe `#1de9b6`.
- Partage équitable = règle LMU Online (chaque pilote ≥ floor(50 % / N des tours de l'équipe)).
- Plugin à 1 Hz vers le site (10 Hz quand quelqu'un regarde la télémétrie).

## Refusé (ne pas reproposer)

- Exclure un tour avec un bouton du volant ; marquer les tours sous FCY.
- Date de la dernière séance par pilote ; tours avant un seuil d'usure ; jour et nuit côte à côte.
- Mur : onglet télémétrie dédié, référence d'essai perso, reconstitution des relais adverses, journal de course, courbe
  d'énergie, replay, voix, alerte prochain pilote, bac à sable, OBS, simulation du classement final, coût de
  l'économie d'énergie, coût de l'usure, undercut, cases devant/derrière, bande de rejoin par tour, fantôme sur la carte.
- Couleurs selon la performance sur la piste à plat.
- Mur « tout autour de la time line » et mur v1 (« ressemble à ce qu'on a déjà fait »).
- Un dash Dash Studio qui ne ressemble pas à la maquette validée.


## Centre cockpit (27/11/2026)

- Organisation validée : Dashs & Overlays, Commandes & Black box (Stream Deck inclus), Profils voiture, Mur des stands, Mémo et partage équipe. Conserver la DA actuelle du site et les fonctionnalités existantes.
- Dash Studio et Control Mapper restent les référentiels natifs ; pas de moteur d'affichage ni de mapping parallèle. Objectif de gestion directe depuis le plugin uniquement après vérification de la compatibilité avec SimHub réel. Remplace le principe antérieur selon lequel l'association des boutons ne devait se faire que dans Control Mapper, sans autoriser d'écriture non vérifiée.
- Mur des stands : chantier d'audit indépendant, pas de refonte visuelle dans la réorganisation.
