# Méthode de travail commune aux IA

Ce guide s'adresse à ChatGPT, Claude, GitHub Copilot et tout autre agent autorisé. GitHub est la référence documentaire ; les conversations privées ne sont pas partagées. Aucun système de dialogue ou d'attribution automatique entre IA.

## Avant toute intervention

1. Lire `AGENTS.md` puis [l'index](README.md) et les documents utiles. [decisions.md](decisions.md) contient les choix validés ; vérifier le code actuel si une décision ou une documentation est datée.
2. Partir de `main` à jour. Vérifier les PR ouvertes, leurs diffs, commentaires et résultats CI avant toute reprise ; un seul chantier fonctionnel à la fois. Ne pas reprendre une branche utilisée par un autre agent sans coordination explicite.
3. Distinguer audit (lecture seule), proposition (pas de code avant validation) et réalisation. Pour une nouvelle fonction ou un écran, proposer une maquette dans `maquettes/` et attendre l'accord. Une petite correction demandée peut être réalisée directement.
4. Nathan ne code pas : il décrit ses besoins en français, affine par itérations, décide et teste. Prendre en charge la réalisation technique, la compilation et les contrôles accessibles ; ne lui demander que les choix ou essais qui requièrent réellement son intervention.

## Pendant le travail

- Tout passe par GitHub ; ne jamais utiliser les anciennes copies du PC. Travailler sur une branche et une PR vers `main`. Préserver les comportements LMU et SimHub validés en piste, les modules autonomes et le lien facultatif au site.
- Préférer une solution simple et maintenable ; pas de dépendance, service ou coût récurrent sans accord. Ne pas exposer de secrets ou demander de nouveaux accès de production.
- Respecter le contrat commun avec le site et le relais : [lien-avec-le-site.md](lien-avec-le-site.md). Pour les changements de format, le site doit être prêt en premier. Si l'accès au dépôt du site manque, le signaler plutôt que deviner.
- Respecter les procédures Windows CI, façade SimHub, ApiCheck, rejeu et photos : [tests-et-ci.md](tests-et-ci.md). Ne pas contourner les tests ni changer les références pour masquer une régression.

## Avant de considérer le chantier terminé

- Vérifier le diff et les contrôles CI. Distinguer tests passés, échoués, non exécutés, rejeu, aperçu et essais réels dans LMU/SimHub. Signaler les nouvelles signatures SimHub non validées par ApiCheck.
- Mettre à jour dans la même PR les documents concernés lorsque le comportement, le contrat, la procédure ou une décision durable évolue. Dater les nouvelles décisions et indiquer les anciennes qu'elles remplacent. Ne pas copier les conversations entières.
- Dans la PR, indiquer l'objectif, les modifications, les décisions utiles, les tests et résultats, les limites, le zip de test lorsqu'il existe et les prochaines étapes. Les problèmes non résolus restent dans la PR ou une issue liée ; cette trace permet une reprise manuelle.
- Donner un bilan en français clair et les quelques essais que Nathan doit faire. Si une action est inaccessible, le dire. Ne jamais lui demander de coder ou d'exécuter des commandes si l'agent peut les réaliser.
- Pas de fusion dans `main` sans accord de Nathan. Ne pas monter la version pour une modification purement documentaire. Une nouvelle version suit [publier.md](publier.md) ; la distribution via le site nécessite ensuite la validation dev et la mise en production du site.

## Autre dépôt

Le site possède sa propre base commune et sa branche `dev` : [app-endurance-manager](https://github.com/boulangernathan1705-cmyk/app-endurance-manager). Ses règles ne remplacent pas celles du plugin. Aucun tableau obligatoire, bot, orchestration, tâche de fond ou abonnement supplémentaire.
