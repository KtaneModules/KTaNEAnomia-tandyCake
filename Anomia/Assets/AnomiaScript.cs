using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class AnomiaScript : MonoBehaviour {

    public KMBombInfo Bomb;
    public KMAudio Audio;

    public KMSelectable nextButton;

    public GameObject[] cards;
    public TextMesh[] texts;
    public SpriteRenderer[] symbols;

    public GameObject[] backings;
    public Material[] backingColors;
    
    public KMSelectable[] iconButtons;
    public SpriteRenderer[] sprites;
    public Sprite[] allSymbols;
    public Sprite[] allModules;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;
    private string[] allTexts = { "Starts with a\nvowel", "Ends with a\nvowel", "Has at least\n1 space", "Has 2 or fewer\nvowels", "Ends with the\nletter 's'", "Does not\ncontain 'e'", "Has no spaces", "Starts with a\nletter from A-M" };
    string vowels = "AEIOU";
    int[] numbers = { 0, 1, 2, 3, 4, 5, 6, 7 };

    int[] symbolIndices = new int[4];
    int[] stringIndices = new int[4];
    bool[] showingBack = new bool[4];
    bool[] isAnimating = new bool[4];

    bool isFighting;
    int fighter;
    int opponent; 
    int stage = 0;

    string[] directions = new string[] { "top", "right", "bottom", "left" };

    void Awake ()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable button in iconButtons) 
        {
            button.OnInteract += delegate () { Press(Array.IndexOf(iconButtons, button)); return false; };
        }
        nextButton.OnInteract += delegate () { Next(); return false; };
        numbers.Shuffle();
    }

    void Start()
    {
        for (int i = 0; i < 4; i++)
        {
            StartCoroutine(MonkiFlip(i));
        }
        Debug.LogFormat("[Anomia #{0}] INITIAL DISPLAY", moduleId);
        Debug.LogFormat("[Anomia #{0}] The initial cards have symbols {1}, {2}, {3}, {4}", moduleId, allSymbols[symbolIndices[0]].name, allSymbols[symbolIndices[1]].name, allSymbols[symbolIndices[2]].name, allSymbols[symbolIndices[3]].name);
        Debug.LogFormat("[Anomia #{0}] The initial messages are {1}, {2}, {3}, {4}", moduleId, allTexts[stringIndices[0]].Replace('\n', ' '), allTexts[stringIndices[1]].Replace('\n', ' '), allTexts[stringIndices[2]].Replace('\n', ' '), allTexts[stringIndices[3]].Replace('\n', ' '));

    }

    void Press(int pos)
    {
        if (!isFighting)
        {
            GetComponent<KMBombModule>().HandleStrike();
            Debug.LogFormat("[Anomia #{0}] Attempted to press a module while no match was active. Strike incurred.", moduleId);
        }
        else if (CheckValidity(opponent, SpriteNames(pos)))
        {
            isFighting = false;
            StartCoroutine(MonkiFlip(opponent));
            CheckFights();
            GenerateIcons();
        }
        else
        {
            GetComponent<KMBombModule>().HandleStrike();
            Debug.LogFormat("[Anomia #{0}] Attempted to choose a module which did not fit the category. Strike incurred.", moduleId);
            StartCoroutine(MonkiFlip(fighter));
            CheckFights();
            GenerateIcons();
        }
    }
    void Next()
    {
        if (moduleSolved || isAnimating.Any(x => x)) return;
        if (!isFighting)
        {
            StartCoroutine(MonkiFlip(stage % 4));
            backings[stage % 4].GetComponent<MeshRenderer>().material = backingColors[1];
            backings[(stage + 3) % 4].GetComponent<MeshRenderer>().material = backingColors[0];
            CheckFights();
            GenerateIcons();
        }
        else
        {
            GetComponent<KMBombModule>().HandleStrike();
            Debug.LogFormat("[Anomia #{0}] Arrow button pressed while a match was happening. Strike incurred.", moduleId);
        }
    }

    void GenerateIcons()
    {
        bool isValid = false;
        allModules.Shuffle();
        Sprite[] tempsprites = new Sprite[4];
        for (int i = 0; i < 3; i++)
        {
            tempsprites[i] = allModules[i];
        }
        if (isFighting) tempsprites[3] = allModules.Where(x => CheckValidity(opponent, x.name.Replace('_', '’'))).PickRandom();
        else tempsprites[3] = allModules[3];
        tempsprites.Shuffle();
        for (int i = 0; i < 4; i++)
        {
            sprites[i].sprite = tempsprites[i];
        }

        if (isFighting)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                if (CheckValidity(opponent, sprites[i].sprite.name.Replace('_', '’'))) names.Add(sprites[i].sprite.name);
            }
            Debug.LogFormat("[Anomia #{0}] The displayed module names are {1}, {2}, {3}, and {4}", moduleId, SpriteNames(0), SpriteNames(1), SpriteNames(2), SpriteNames(3));
            Debug.LogFormat("[Anomia #{0}] The correct possible modules are {1}", moduleId, names.Join(", "));
        }
    }

    void CheckFights()
    {
        Debug.Log(stage);
        if (symbolIndices.Distinct().Count() != 4)
        {
            isFighting = true;
            int duplicate = symbolIndices.Where(x => symbolIndices.Count(y => y == x) == 2).First(); // how tf does this line of code work I'm gonna hurl
            for (int i = 0; i < 4; i++ )
            {
                int num = (i + stage) % 4;
                if (symbolIndices[num] == duplicate) { fighter = num; break; }
            }
            for (int i = 0; i < 4; i++ )
            {
                int num = (i + stage) % 4;
                if (num != fighter && symbolIndices[num] == duplicate) { opponent = num; break; }
            }
            Debug.LogFormat("[Anomia #{0}] A match has started between the {1} player and the {2} player.", moduleId, directions[fighter], directions[opponent]);
        }
        else
        {
            stage++;
            Debug.LogFormat("[Anomia #{0}] STAGE {1}", moduleId, stage);
        }

    }

    bool CheckValidity(int cardNum, string input)
    {
        name = input.ToUpper();
        switch (stringIndices[cardNum])
        {
            case 0: if (vowels.Contains(name.First())) return true; break;
            case 1: if (vowels.Contains(name.Last()))  return true; break;
            case 2: if (name.Contains(' ')) return true; break;
            case 3: if (name.Count(x => vowels.Contains(x)) <= 2) return true; break;
            case 4: if (name.Last() == 'S') return true; break;
            case 5: if (!name.Contains('E')) return true; break;
            case 6: if (!name.Contains(' ')) return true; break;
            case 7: if ("ABCDEFGHIJKLM".Contains(name.First())) return true; break;
        }
        return false;
    }

    IEnumerator MonkiFlip(int pos)
    {
        if (isAnimating[pos]) yield break;
        isAnimating[pos] = true;
        Audio.PlaySoundAtTransform("Flip", cards[pos].transform);
        int faceAffected = showingBack[pos] ? pos : pos + 4;
        symbolIndices[pos] = (stage == 0) ? numbers[pos] : UnityEngine.Random.Range(0, 8);
        stringIndices[pos] = UnityEngine.Random.Range(0, allTexts.Length);
        texts[faceAffected].text = allTexts[stringIndices[pos]];
        symbols[faceAffected].sprite = allSymbols[symbolIndices[pos]];
        //if (stage != 0) Debug.LogFormat("[Anomia #{0}] The {1} card flipped over. Its symbol is {2} and its message is {3}", moduleId, directions[pos], allSymbols[symbolIndices[pos]].name, allTexts[stringIndices[pos]]);
        isAnimating[pos] = true;
        showingBack[pos] = !showingBack[pos];
        Transform TF = cards[pos].transform;
        while (TF.localPosition.z > -6)
        {
            TF.localPosition += new Vector3(0, 0, -0.2f);
            TF.localEulerAngles += new Vector3(0, -3f, 0);
            yield return null;
        }
        while (TF.localPosition.z < -0.05f)
        {
            TF.localPosition -= new Vector3(0, 0, -0.2f);
            TF.localEulerAngles += new Vector3(0, -3f, 0);
            yield return null;
        }
        isAnimating[pos] = false;
    }
    
    string SpriteNames(int pos)
    {
        return sprites[pos].sprite.name.Replace('_', ' ');
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use !{0} to do something.";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand (string Command)
    {
      yield return null;
    }

    IEnumerator TwitchHandleForcedSolve ()
    {
      yield return null;
    }
}
