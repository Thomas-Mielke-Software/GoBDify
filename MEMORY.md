# Projekt-Konventionen für KI-Assistenz

## Git-Commit-Attribution

In Commit-Messages, die unter KI-Assistenz entstehen, wird der Trailer
`AI-Assisted-By:` verwendet — nicht `Co-Authored-By:`. Begründung:
„Autor"/„Co-Autor" tragen Konnotationen von Verantwortung, Persistenz und
moralischem Subjektstatus, die für eine inferenz-zeitige Modell-Instanz
nicht zutreffen. `AI-Assisted-By:` markiert die Provenienz ohne diese
Überbehauptung.

Format:

```
AI-Assisted-By: Claude <Modell> <noreply@anthropic.com>
```

Beispiel:

```
Fix: Foobar in der Bar-Klasse korrigiert

Bla bla bla.

AI-Assisted-By: Claude Opus 4.7 <noreply@anthropic.com>
```
