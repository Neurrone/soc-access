Place non-English mod translation files here as .po files.

Files are named for the game's `CurrentLanguage.LanguageCode` values and deployed to:

`BepInEx/config/SongsOfConquestAccess/translations`

Examples:

- fr.po
- de.po
- es.po
- pt-BR.po
- zh-CN.po

English strings live in code as fallbacks.

Entries use `msgctxt` as the mod string key:

```po
#. Draft.PurchaseForResources
msgctxt "Draft.PurchaseForResources"
msgid "Purchase for {0}"
msgstr ""
```

Plural entries are split into one key per plural form. Translate every plural key that exists in your language file.
The number after the key is the plural form index selected at runtime:

- `_0`: singular form for languages that have one, or the only form for Chinese, Japanese, Korean, and Turkish
- `_1`: regular plural form for English-like languages
- `_2`: extra plural form used by Polish, Russian, and Ukrainian

```po
msgctxt "Example.TurnCount_0"
msgid "{0} turn"
msgstr ""

msgctxt "Example.TurnCount_1"
msgid "{0} turns"
msgstr ""
```

For Polish, Russian, and Ukrainian, a third entry may also appear:

```po
msgctxt "Example.TurnCount_2"
msgid "{0} turns"
msgstr ""
```

Keep placeholders such as `{0}` exactly as they appear in `msgid`; validation fails if a translation drops or changes them.
