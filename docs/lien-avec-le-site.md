# Lien avec le site

Le site [Endurance Manager](https://endurance-manager.app) (dépôt public
[app-endurance-manager](https://github.com/boulangernathan1705-cmyk/app-endurance-manager), Cloudflare Workers + D1)
garde tout le côté serveur. Ce dépôt ne contient que le plugin.

## Liaison

- Le pilote copie son code sur la page Télémétrie Mémo (fiches combo) du site :
  `EMCOMBO1 <adresse du site> <clé de 64 caractères> EMCOMBO1` (`Sync.cs`, `LinkCode`). Le plugin le détecte aussi dans
  le presse-papiers.
- Chaque appel envoie `Authorization: Bearer <clé>`. Le 401 suspend la file de cette liaison. Le 403 et les échecs temporaires la conservent pour un nouvel essai.
- Module du site : `combos` (privé, équipe FMT). Côté site : `server/combos.mjs`.

## Routes utilisées

| Route | Sens | Module |
| --- | --- | --- |
| `POST /api/combos/plugin/session` | une séance (tours, arrêts, service, prévision du jeu) | Télémétrie Mémo (`Sync.cs`) |
| `GET /api/combos/plugin/sheet` | la fiche du combo (moyennes par pilote) | Overlay relais (`Practice.cs`) |
| `WS /api/combos/plugin/live` (+ `/pass`) | la voiture et le classement, 1 Hz | Mur des stands équipages (`LiveShare.cs`) |
| `WS /api/combos/plugin/live/watch` (+ `/pass`) | suivre le salon en spectateur | mur dans SimHub par le site |
| `POST /api/combos/plugin/report` | journal ou signalement | Aide au développement (`Report.cs`) |
| `GET /downloads/simhub/v0/plugin.json`, `installer.cmd`, `EnduranceManager.SimHub.dll` | mise à jour (versions 0.x) | `Update.cs`, installeur |

`/pass` donne l'adresse du relais temps réel (Durable Object) pour quelques minutes, ou 404 : on se connecte alors au
site lui-même.

## Séances en attente

La liaison est mémorisée au début de la séance. Les fichiers sont isolés par adresse et clé, dont seule l’empreinte sert
de nom de dossier. Un changement de code, même sur le même site, conserve l’ancienne file à part. Les séances sans code
et les anciens fichiers sans destination restent sur le PC. Un accusé JSON du serveur confirme une création (201) ou une
réception antérieure (200). Les refus définitifs d’une séance restent dans le sous-dossier `refused` de sa file.

## Formats (contrat)

- Séances : `cleanSession` dans `shared/combos.mjs` du site.
- Temps réel : `cleanSnapshot`, `FIELD` (ordre des colonnes de chaque voiture) dans `shared/live.mjs` du site. Le rejeu
  de ce dépôt télécharge ce fichier depuis la branche `dev` du site (`SITE_REF` dans `.github/workflows/plugin.yml`).
- Ajouter un champ : d'abord le site (qui l'accepte et l'ignore s'il manque), déployé ; ensuite le plugin.

## Mise à jour automatique

- Numérotation repartie de **0.1.0** le 22/11/2026 (avant : 2.x, jusqu'à 2.6.0). Les 0.x lisent leur propre dossier du
  site, `/downloads/simhub/v0/`.
- Le site garde la dernière version dans `connectors/simhub-plugin/v0/` (DLL, `plugin.json` de la Release tel quel,
  `installer.cmd`), remplacé par la PR que la CI ouvre à chaque Release ([publier.md](publier.md)). `scripts/build.mjs`
  le sert dans `/downloads/simhub/v0/`, et dans `/downloads/simhub/` avec la version `2.99.0` : les anciens plugins 2.x
  y voient une version plus récente et passent une fois à la 0.x ; une fois en 0.x, le plugin ne lit plus que `v0/`,
  donc pas de boucle. L'ancienne 2.2.0 (`release/`) a été retirée du site le 23/11/2026.
- Le plugin lit `https://<site>/downloads/simhub/v0/plugin.json` au plus une fois par heure (le site du code de
  liaison, ou `https://endurance-manager.app` sans liaison). Version plus récente : « Mettre à jour » télécharge la DLL
  du même dossier, la vérifie (taille + SHA-256 de `plugin.json`) et la met à la place, puis SimHub redémarre
  (`Update.cs`). La page Télémétrie Mémo donne l'installeur de `v0/` avec l'adresse du site écrite dedans.
- Une vérification impossible (site injoignable, dossier absent) s'affiche « Non vérifié » dans Mises à jour.
