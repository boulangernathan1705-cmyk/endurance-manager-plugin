# Profil Stream Deck « Endurance Manager · FMT »

Stream Deck MK.2 (15 touches). Maquette validée par Nathan le 24/11/2026 : https://claude.ai/artifact/Gi7MJDFribekYZYFzec85g

![Aperçu](apercu.png)

## Installer (pilote)

1. Dans le logiciel Stream Deck, installer les plugins **Simhub ControlMapper** et **Stream Deck SimHub** (les mêmes
   que pour le profil de FMT companion).
2. Plugin Endurance Manager : activer **Black box sur boutons** (rôles « FMT - … ») et **Réglages voiture en course**
   (rôles « EM - Profil 1/2/3 »).
3. Double-cliquer `Endurance Manager FMT.streamDeckProfile`.

## Ce qu'il montre

- Accueil : les 3 profils (vert = actif, « conseillé » quand l'humidité de la piste le demande), demande d'arrêt,
  limiteur, phares, essuie-glace, freins, réparations, appel de phares, et les dossiers Voiture, Stand, Pneus, Caméra,
  Poste.
- Voiture et Stand : un réglage par colonne, plus en haut, valeur en direct au milieu, moins en bas.
- Une couleur par famille : Voiture rouge, Stand orange, Pneus jaune, Poste vert, Caméra bleu.
- Valeurs en direct : propriétés SimHub (`DataCorePlugin.GameData.*`), FMT companion (`FMTCompanionPlugin.Pit.*`) et
  plugin (`EnduranceManagerPlugin.Profils.N.Titre`, `.Actif`). Coupure TC, TC glisse, régén, migration et antiroulis :
  LMU ne les donne pas à SimHub, la touche du milieu montre seulement le nom.

## Refaire le profil

`python3 streamdeck/build.py` (avec `cairosvg` et `pillow`) réécrit le profil et `apercu.png`. Les touches sont
décrites dans `PAGES` de `build.py`.

Les boutons « dynamiques » de SimHub 9.13.6 demandent leur propre plugin Stream Deck (logiciel Stream Deck 7.6) : à
reprendre quand les pilotes l'auront.
