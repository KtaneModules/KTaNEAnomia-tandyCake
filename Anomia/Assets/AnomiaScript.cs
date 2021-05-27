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

    private string[] allTexts = new string[] { "Starts with a\nvowel", "Ends with a\nvowel", "Has at least\n1 space", "Ends with the\nletter 's'", "Does not\ncontain 'e'", "Has no spaces", "Starts with a\nletter from A-M", "Ends in a\nletter from A-M", "Has repeated\nletters", "Has no\nrepeated letters" };
    string vowels = "AEIOU";
    string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    string[] directions = new string[] { "top", "right", "bottom", "left" };
    int[] numbers = Enumerable.Range(0, 8).ToArray();

    int[] symbolIndices = new int[4];
    int[] stringIndices = new int[4];
    bool[] showingBack = new bool[4];
    bool[] isAnimating = new bool[4];

    bool isFighting;
    int fighter;
    int opponent; 
    int stage = 0;

    float timeLimit = 10f;
    float warning = 3f;
    private Coroutine timer;

    public bool TwitchPlaysActive;
    float TPTime = 30f;
    float TPWarning = 7f;

    void Awake ()
    {
        allModules = allModules.Where(x => x != null).ToArray();
        moduleId = moduleIdCounter++;
        foreach (KMSelectable button in iconButtons) 
            button.OnInteract += delegate () { Press(Array.IndexOf(iconButtons, button)); return false; };
        nextButton.OnInteract += delegate () { Next(); return false; };
        GetComponent<KMBombModule>().OnActivate += delegate () { StartCoroutine(CheckTP()); };
        numbers.Shuffle();
    }
    
    void Start()
    {
        StartCoroutine(CheckTP());
        for (int i = 0; i < 4; i++)
        {
            StartCoroutine(MonkiFlip(i));
        }
        Debug.LogFormat("[Anomia #{0}] INITIAL DISPLAY", moduleId);
        Debug.LogFormat("[Anomia #{0}] The initial cards have symbols {1}.", moduleId, symbolIndices.Select(x => allSymbols[x].name).Join(", "));
        Debug.LogFormat("[Anomia #{0}] The initial messages are {1}.", moduleId, allTexts.Select(x => x.Replace('\n', ' ')).Join(", "));
        timer = StartCoroutine(Timer());
        StopCoroutine(timer);
    }

    void Press(int pos)
    {
        iconButtons[pos].AddInteractionPunch(0.3f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, iconButtons[pos].transform);

        StopCoroutine(timer);
        if (moduleSolved || isAnimating.Any(x => x)) return;
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
            StopCoroutine(timer);
            Debug.LogFormat("[Anomia #{0}] Attempted to choose a module which did not fit the category. Strike incurred.", moduleId);
            StartCoroutine(MonkiFlip(fighter));
            CheckFights();
            GenerateIcons();
        }
    }
    void Next()
    {
        nextButton.AddInteractionPunch(1);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, nextButton.transform);

        if (moduleSolved || isAnimating.Any(x => x)) return;
        else if (!isFighting)
        {

            if (stage == 12)
            {
                StartCoroutine(Solve());
                return;
            }
            StartCoroutine(MonkiFlip(stage % 4));
            backings[stage % 4].GetComponent<MeshRenderer>().material = backingColors[1];
            backings[(stage + 3) % 4].GetComponent<MeshRenderer>().material = backingColors[0];
            stage++;
            Debug.LogFormat("[Anomia #{0}] ::STAGE {1}::", moduleId, stage); 
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
        allModules.Shuffle();
        Sprite[] tempsprites = new Sprite[4];
        for (int i = 0; i < 3; i++)
        {
            tempsprites[i] = allModules[i];
        }
        if (isFighting) tempsprites[3] = allModules.Where(x => x != null && CheckValidity(opponent, x.name.Replace('_', '’'))).PickRandom();  
        else tempsprites[3] = allModules[3]; //If we are in a match, makes sure at least one icon is valid. Otherwise, generate 4 random icons.
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
                if (CheckValidity(opponent, SpriteNames(i)))
                        names.Add(SpriteNames(i));
            }
            Debug.LogFormat("[Anomia #{0}] The displayed module names are {1}, {2}, {3}, and {4}", moduleId, SpriteNames(0), SpriteNames(1), SpriteNames(2), SpriteNames(3));
            Debug.LogFormat("[Anomia #{0}] The correct possible modules are {1}", moduleId, names.Join(", "));
        }
    }

    void CheckFights()
    {
        if (symbolIndices.Distinct().Count() != 4)
        {
            isFighting = true;
            timer = StartCoroutine(Timer());
            int duplicate = symbolIndices.Where(x => symbolIndices.Count(y => y == x) == 2).First(); // how tf does this line of code work I'm gonna hurl
            for (int i = 0; i < 4; i++)
            {
                int num = (i + stage + 3) % 4;
                if (symbolIndices[num] == duplicate) { fighter = num; break; }
            }
            for (int i = 0; i < 4; i++)
            {
                int num = (i + stage + 3) % 4;
                if (num != fighter && symbolIndices[num] == duplicate) { opponent = num; break; }
            }
            Debug.LogFormat("[Anomia #{0}] A match has started between the {1} player and the {2} player.", moduleId, directions[fighter], directions[opponent]);
        }
        else isFighting = false;
    }

    bool CheckValidity(int cardNum, string input)
    {
        name = input.ToUpper();
        switch (stringIndices[cardNum])
        {
            case 0: if (vowels.Contains(name.First())) return true; break;
            case 1: if (vowels.Contains(name.Last()))  return true; break;
            case 2: if (name.Contains(' ')) return true; break;
            case 3: if (name.Last() == 'S') return true; break;
            case 4: if (!name.Contains('E')) return true; break;
            case 5: if (!name.Contains(' ')) return true; break;
            case 6: if ("ABCDEFGHIJKLM".Contains(name.First())) return true; break;
            case 7: if ("ABCDEFGHIJKLM".Contains(name.Last())) return true; break;
            case 8: if (name.Where(x => alphabet.Contains(x)).Distinct().Count() != name.Where(x => alphabet.Contains(x)).Count()) return true; break;
            case 9: if (name.Where(x => alphabet.Contains(x)).Distinct().Count() == name.Where(x => alphabet.Contains(x)).Count()) return true; break;
        }
        return false;
    }

    IEnumerator Solve()
    {
        moduleSolved = true;
        for (int i = 0; i < 4; i++)
        {
            StartCoroutine(MonkiFlip(i));
            int cardFace = showingBack[i] ? i + 4 : i;
            texts[cardFace].text = string.Empty;
            symbols[cardFace].sprite = null;
            sprites[i].sprite = null;
        }
        yield return new WaitForSeconds(0.5f);
        GetComponent<KMBombModule>().HandlePass();
        Audio.PlaySoundAtTransform("Solve", transform);
        for (int i = 0; i < 4; i++)
        {
            backings[i].GetComponent<MeshRenderer>().material = backingColors[2];
        }
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
        if (stage != 0 && !moduleSolved) Debug.LogFormat("[Anomia #{0}] The {1} card flipped over. Its symbol is {2} and its message is {3}", moduleId, directions[pos], allSymbols[symbolIndices[pos]].name, allTexts[stringIndices[pos]].Replace('\n',' '));
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

    IEnumerator Timer()
    {
        float currentTime = 0f;
        bool playedWarning = false;
        while (true)
        {
            currentTime += Time.deltaTime;
            if (currentTime > timeLimit - warning && !playedWarning)
            {
                Audio.PlaySoundAtTransform("Warning", transform);
                playedWarning = true;
            }
            if (currentTime > timeLimit)
            {
                StopCoroutine(timer);
                GetComponent<KMBombModule>().HandleStrike();
                Debug.LogFormat("[Anomia #{0}] Timer expired, strike incurred.", moduleId);
                StartCoroutine(MonkiFlip(fighter));
                CheckFights();
                GenerateIcons();
            }
            yield return null;
        }
    }
    
    string SpriteNames(int pos)
    {
        return sprites[pos].sprite.name.Replace('_', '’');
    }
    IEnumerator CheckTP()
    {
        yield return null;
        if (TwitchPlaysActive)
        {
            timeLimit = TPTime;
            warning = TPWarning;
            Debug.Log("tp code fired");
        }
    }
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use !{0} press 1/2/3/4 to press the button in that position in reading order. Use {0} next to press the arrow buton. On TP, the timer is extended to 30 seconds.";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand (string input)
    {
        Debug.Log(timeLimit);
        string Command = input.Trim().ToUpperInvariant();
        List<string> parameters = Command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        string[] possibleCommands = { "1", "2", "3", "4", "TL", "TR", "BL", "BR" };
        if (Regex.IsMatch(Command, @"^(press)?\s*([1-4]|([TB][LR]))$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            yield return null;
            iconButtons[Array.IndexOf(possibleCommands, parameters.Last()) % 4].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        else if (parameters.Count == 1 && parameters.First() == "NEXT")
        {
            yield return null;
            nextButton.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }

    IEnumerator TwitchHandleForcedSolve ()
    {
        while (!moduleSolved)
        {
            if (!isFighting)
            {
                if (isAnimating.Any(x => x)) yield return true;
                nextButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    if (CheckValidity(opponent, SpriteNames(i)))
                    {
                        iconButtons[i].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                    }
                }
            }
            yield return null;
        }
    }
}
