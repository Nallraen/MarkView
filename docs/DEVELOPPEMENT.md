# MarkView – documentation développeur

Ce document s'adresse à celles et ceux qui veulent compiler MarkView, comprendre son fonctionnement ou
proposer une modification. Pour utiliser l'application, voir le [README](../README.md).

## Prérequis

- Windows 10 (1809+) ou Windows 11, x64.
- [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).
- [Runtime WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) (présent sur Windows 11).

## Stack

.NET 10 / WPF, [Markdig](https://github.com/xoofx/markdig) (Markdown → HTML),
[WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) (aperçu),
[AvalonEdit](https://github.com/icsharpcode/AvalonEdit) (éditeur),
[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MVVM),
Microsoft.Extensions.DependencyInjection, xUnit pour les tests.

## Compilation, tests et lancement

```bash
dotnet build MarkView.sln
```

```bash
dotnet test MarkView.sln
```

```bash
dotnet run --project src/MarkView -- sample.md
```

## Publication

Exe unique, dépendant du framework (léger, nécessite le runtime .NET 10 Desktop) :

```bash
dotnet publish src/MarkView -c Release -r win-x64
```

Le fichier est produit dans `src/MarkView/bin/Release/net10.0-windows/win-x64/publish/MarkView.exe`.
Le profil équivalent `win-x64` publie dans `src/MarkView/bin/publish/win-x64/` :

```bash
dotnet publish src/MarkView -p:PublishProfile=win-x64
```

Variante autonome (aucun runtime .NET requis, exe plus lourd) :

```bash
dotnet publish src/MarkView -p:PublishProfile=win-x64-self-contained
```


## Publier une nouvelle version (GitHub Actions)

Deux workflows sont prêts dans `.github/workflows/` :

- **`ci.yml`** : compile et lance les tests à chaque push et à chaque pull request sur `main`.
- **`release.yml`** : à chaque tag `vX.Y.Z` poussé, compile, teste, publie les deux exécutables (léger et
  autonome) avec la version du tag, puis crée la release GitHub correspondante avec les fichiers joints.

Pour sortir une version :

```bash
git tag v1.1.0
```

```bash
git push origin v1.1.0
```

## Données de l'application

| Élément | Emplacement |
|---|---|
| Paramètres (JSON) | `%AppData%\MarkView\settings.json` |
| Journaux | `%AppData%\MarkView\logs\` |
| Profil WebView2, assets de l'aperçu extraits | `%LocalAppData%\MarkView\` |

## Architecture

```
MarkView.sln
├── src/MarkView/
│   ├── App.xaml(.cs)        démarrage, injection de dépendances, instance unique, gestion globale des erreurs
│   ├── Models/              AppSettings, énumérations, TextFileContent
│   ├── Services/            Markdown, fichiers, paramètres, thèmes, export, fichiers récents, dialogues…
│   ├── ViewModels/          MainViewModel (partiel : cœur, fichiers, export), DocumentViewModel, SettingsViewModel
│   ├── Views/               MainWindow, SettingsWindow, MessageDialog
│   ├── Controls/            EditorControl (AvalonEdit), PreviewControl (WebView2), panneau de recherche
│   ├── Themes/              Light.xaml, Dark.xaml (palettes), Controls.xaml (styles)
│   └── Assets/              Preview/ (HTML, CSS, highlight.js), Syntax/ (xshd), icône
└── tests/MarkView.Tests/    tests xUnit des services
```

MVVM avec CommunityToolkit.Mvvm ; le code-behind se limite à la colle de vue (mise en page des modes,
placement de fenêtre, glisser-déposer) et l'interop WebView2/AvalonEdit est isolée dans `Controls/`.

## Choix techniques

Décisions prises là où le cahier des charges laissait le choix :

**Structure et MVVM**
- Solution au format `.sln` classique (le SDK .NET 10 crée un `.slnx` par défaut).
- L'énumération des thèmes s'appelle `AppTheme` : `ThemeMode` entre en conflit avec le type WPF du même nom
  apparu dans .NET 9.
- Un seul éditeur et un seul aperçu pour tous les onglets : le `TabControl` ne sert que de barre d'onglets et
  l'onglet actif est affiché dessous. Chaque document garde son `TextDocument` AvalonEdit (texte et historique
  d'annulation) ; la sélection et la position de défilement sont mémorisées par document.
- `MainViewModel` est découpé en fichiers partiels : cœur, fichiers, export.
- Le « mode d'affichage par défaut » des paramètres et le mode courant sont un seul et même réglage : le dernier
  mode utilisé est celui du prochain démarrage.
- Les raccourcis sont des `KeyBinding` de la fenêtre. Ceux de formatage visent l'éditeur et ignorent **AltGr**,
  que Windows signale comme Ctrl+Alt : sur un clavier AZERTY, AltGr+3 (`#`) et AltGr+2 (`~`) déclencheraient
  sinon Titre 3 et Titre 2. Le raccourci `Ctrl+I` d'AvalonEdit (indentation) a été retiré au profit de l'italique.
  `Ctrl+F4` ferme aussi l'onglet.
- Les nombres de la barre d'état suivent la culture Windows (« 5 015 », « 100 % »).
- Nombre de mots : suites de caractères séparées par des blancs, dans le source Markdown. Nombre de caractères :
  sauts de ligne exclus.

**Aperçu**
- Le rendu Markdown → HTML se fait hors du thread UI, sur un instantané immuable du document, 250 ms après la
  dernière frappe (environ 250 ms pour 1 Mo). Seule la dernière version est envoyée à la page.
- `preview.html` est servi par l'hôte virtuel `https://app.markview.example/`. Les assets sont embarqués dans
  l'exe et extraits au premier lancement dans `%LocalAppData%\MarkView\preview\<empreinte>\`, ce qui garde un exe
  unique tout en permettant `SetVirtualHostNameToFolderMapping`.
- Les images relatives sont servies par un gestionnaire `WebResourceRequested` sur `https://doc.markview.example/`.
  Le chemin complet du dossier du document figure dans l'URL et sert de `<base>`.
  - Pourquoi : une correspondance d'hôte virtuel ajoutée après le chargement de la page ne s'applique qu'à la
    navigation suivante, et on ne recharge jamais la page.
  - Seuls les fichiers image, audio et vidéo sont servis. `../images/x.png` fonctionne.
- Sécurité : une CSP n'autorise que les scripts de l'application. Les `<script>`, les attributs `on…=` et les URL
  `javascript:` présents dans le Markdown ne s'exécutent pas, les iframes sont bloquées, et toute navigation de la
  page est annulée. Le HTML exporté reçoit la même règle (scripts autorisés par nonce). Les menus contextuels, le
  zoom du navigateur et ses raccourcis sont désactivés, ainsi que les DevTools en Release.
- highlight.js ne colore que les blocs qui déclarent un langage, comme GitHub ; les résultats sont mis en cache.
- Scroll synchronisé : bloc dont le `data-line` est le plus grand inférieur ou égal à la première ligne visible de
  l'éditeur, avec interpolation vers le bloc suivant. Le sens aperçu → éditeur (bonus) n'est pas implémenté : il
  n'aurait pas été fiable avec le retour à la ligne et les blocs hors ordre comme les notes.
- La recherche dans l'aperçu utilise `window.find` (barre de recherche au-dessus de l'aperçu).

**Éditeur**
- Le formatage est fait de fonctions pures (`MarkdownFormatter`). Chaque action est une seule étape d'annulation
  et respecte la fin de ligne du document.
- Les marqueurs `*` et `**` sont distingués par la longueur de la suite d'étoiles. Un titre de même niveau
  appliqué de nouveau est retiré.
- La ligne horizontale et le tableau sont précédés d'une ligne vide, pour ne pas transformer le paragraphe du
  dessus en titre « setext ».
- Rechercher/Remplacer : panneau maison, car le `SearchPanel` d'AvalonEdit ne sait pas remplacer.
  - Surlignage de toutes les occurrences et nombre de résultats.
  - Une expression régulière invalide est signalée sans erreur ; délai maximal de 2 s.
  - « Tout remplacer » se fait en une seule étape d'annulation.
- Les liens cliquables d'AvalonEdit sont désactivés : leur `Process.Start(url)` ne fonctionne pas sous .NET moderne.

**Fichiers**
- Ordre de détection de l'encodage : BOM (UTF-8, UTF-32 LE, UTF-16 LE/BE), puis UTF-8 strict, puis Windows-1252
  en repli. Enregistrement toujours en UTF-8 sans BOM.
- La fin de ligne dominante est conservée ; un fichier aux fins de ligne mixtes est unifié vers la dominante.
- Enregistrement sûr : écriture dans un fichier temporaire du même dossier puis remplacement (`File.Replace`
  conserve les attributs), avec repli sur une écriture directe.
- Surveillance externe : un `FileSystemWatcher` par document, avec 300 ms d'anti-rebond.
  - Le contenu du disque est comparé au dernier contenu lu ou écrit, ce qui ignore nos propres enregistrements.
  - Le rechargement ne remplace que la partie modifiée, en une étape annulable.
  - Un fichier supprimé à l'extérieur garde son onglet et passe en « modifié ».
- Instance unique : mutex `Local\MarkView.SingleInstance.<SID>` et pipe nommé réservé à l'utilisateur courant.
  L'instance secondaire envoie des chemins complets. Si le pipe ne répond pas, l'application démarre normalement
  plutôt que de perdre la demande.
- Un document « Sans titre » vide et intact est remplacé par le premier fichier ouvert. Fermer le dernier onglet
  crée un document vide. Les fichiers récents sont enregistrés dès qu'ils changent.

**Thèmes, paramètres, export**
- Thème : la palette (`Themes/Light.xaml` / `Dark.xaml`) est remplacée à chaud et toutes les couleurs passent
  par des `DynamicResource`.
  - Le mode Système lit `AppsUseLightTheme` et écoute `SystemEvents.UserPreferenceChanged`.
  - La barre de titre utilise l'attribut DWM 20, avec repli sur 19.
- Paramètres : JSON indenté, énumérations en texte, enregistrement atomique, valeurs hors limites corrigées.
  - Un fichier corrompu est copié en `settings.json.bak` et les valeurs par défaut sont utilisées.
  - La liste des polices ne propose que les polices à chasse fixe installées ; on peut en saisir une autre.
- Visibilité de la fenêtre au démarrage : la position mémorisée est vérifiée par rapport à l'écran virtuel
  (ensemble des moniteurs). Sinon, la fenêtre est centrée.
- PDF et impression : rendu en thème clair dans un WebView2 invisible (jamais affiché à l'écran), images intégrées.
  - PDF au format A4, marges de 15 mm, sans en-tête ni pied de page.
  - L'impression passe par la boîte de dialogue d'impression de Windows (imprimante, copies, orientation) puis
    `CoreWebView2.PrintAsync`.
- Copier en HTML : format `CF_HTML` (décalages en octets UTF-8) et texte brut contenant le HTML.

**Limites connues**
- Le sens de synchronisation aperçu → éditeur n'est pas implémenté (voir plus haut).
- Les formules mathématiques s'affichent en TeX brut (pas de MathJax embarqué). Les vidéos intégrées (iframes)
  sont bloquées par la CSP.
- La continuation de liste s'applique aussi à l'intérieur d'un bloc de code délimité par ```.
- En mode regex, `$` ne correspond pas juste avant `\r` dans un fichier CRLF (comportement de .NET).
- Sur certaines versions de Windows 10, la barre de titre d'une fenêtre déjà ouverte ne change de couleur qu'après
  une perte de focus.
- Une fenêtre placée dans le « trou » d'une disposition multi-écrans en L peut passer la vérification de
  visibilité.
- L'impression n'a pas été testée sur une imprimante physique (l'export PDF, qui utilise le même rendu, l'a été).

## Licence

Code sous [PolyForm Noncommercial 1.0.0](../LICENSE.md) ; composants tiers : voir
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).
