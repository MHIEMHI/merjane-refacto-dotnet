### Consignes .NET: 
* Ignorez les migrations BDD
* Ne pas modifier les classes qui ont un commentaire: `// WARN: Should not be changed during the exercise
`
* Pour lancer les tests: `dotnet test`

### Architecture
* `Refacto.DotNet.Domain`: entités et types de domaine indépendants de l'infrastructure.
* `Refacto.DotNet.Application`: contrats et cas d'utilisation d'orchestration et de règles produit.
* `Refacto.DotNet.Infrastructure`: persistance Entity Framework et adaptateurs techniques.
* `Refacto.DotNet.Controllers`: API HTTP et composition des dépendances.

Les dépendances vont vers le cœur: l'API utilise l'application, l'infrastructure implémente les contrats de l'application, et l'application dépend du domaine.

### Résumé des changements
* Séparation de l'API, de l'application, du domaine et de l'infrastructure en projets distincts.
* Extraction des règles de traitement par type de produit dans des handlers dédiés, sélectionnés via une stratégie et enregistrés par injection de dépendances.
* Centralisation de l'orchestration des commandes: traitement des produits, sauvegarde unique après traitement réussi, puis envoi des notifications.
* Comparaisons saisonnières et d'expiration effectuées sur des dates calendaires, avec une horloge injectable pour rendre les tests déterministes.
* Accès asynchrone à la base de données avec propagation de l'annulation. L'API renvoie `404` si la commande est absente et `400` si l'identifiant n'est pas valide.
* Ajout et renforcement des tests unitaires et d'intégration pour les règles métier, les limites de dates, les erreurs et le comportement observable de l'API.