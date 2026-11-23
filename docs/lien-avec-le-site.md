# Lien avec le site

Le site [Endurance Manager](https://endurance-manager.app) (dépôt public
[app-endurance-manager](https://github.com/boulangernathan1705-cmyk/app-endurance-manager), Cloudflare Workers + D1)
garde tout le côté serveur. Ce dépôt ne contient que le plugin.

## Liaison

- Le pilote copie son code sur la page Télémétrie Mémo (fiches combo) du site :
  `EMCOMBO1 <adresse du site> <clé de 64 caractères> EMCOMBO1` (`Sync.cs`, `LinkCode`). Le plugin le détecte aussi dans
  le presse-papiers.
- Chaque appel envoie `Authorization: Bearer <clé>`. Clé retirée (401) ou droit perdu (403) : le plugin oublie le code.
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

## Formats (contrat)

- Séances : `cleanSession` dans `shared/combos.mjs` du site.
- Temps réel : `cleanSnapshot`, `FIELD` (ordre des colonnes de chaque voiture) dans `shared/live.mjs` du site. Le rejeu
  de ce dépôt télécharge ce fichier depuis la branche `dev` du site (`SITE_REF` dans `.github/workflows/plugin.yml`).
- Ajouter un champ : d'abord le site (qui l'accepte et l'ignore s'il manque), déployé ; ensuite le plugin.

## Mise à jour automatique

- Numérotation repartie de **0.1.0** le 22/11/2026 (avant : 2.x, jusqu'à 2.6.0). Les 0.x lisent leur propre dossier du
  site, `/downloads/simhub/v0/`, sinon ils prendraient l'ancienne 2.2.0 servie dans `/downloads/simhub/` pour une
  version plus récente.
- Le plugin lit `https://<site>/downloads/simhub/v0/plugin.json` au plus une fois par heure (le site du code de
  liaison, ou `https://endurance-manager.app` sans liaison). Version plus récente : bouton « Mettre à jour », qui
  télécharge `installer.cmd` du même dossier et le lance (ferme SimHub, copie la DLL vérifiée par taille + SHA-256,
  relance SimHub). Tant que le site n'a pas ce dossier : pas de mise à jour proposée, on installe depuis les Releases.
- Passer les plugins 2.x déjà installés à la 0.x : dans `connectors/simhub-plugin/release/` du site, mettre la DLL
  0.x avec `{"version":"2.99.0"}` dans `plugin.json` (l'installeur vérifie taille et SHA-256, pas la version de la DLL).
  Les 2.x y voient une version plus récente et l'installent ; une fois en 0.x, le plugin ne lit plus que `v0/`, donc
  pas de boucle. Jusque-là, `/downloads/simhub/` (2.2.0) reste tel quel. À terminer côté site au premier
  envoi d'une 0.x aux pilotes : servir `connectors/simhub-plugin/v0/` dans `/downloads/simhub/v0/` (`scripts/build.mjs`).
