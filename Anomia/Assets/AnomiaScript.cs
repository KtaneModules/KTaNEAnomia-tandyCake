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
    public KMBombModule Module;
    public KMGameInfo GameInfo;

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

    private string[] allTexts = new string[] { "Starts with a\nvowel", "Ends with a\nvowel", "Has at least\n1 space", "Ends with the\nletter 's'", "Does not\ncontain 'e'", "Has no spaces", "Starts with a\nletter from A-M", "Ends in a\nletter from A-M", "Contains fewer\nthan 8 letters", "Contains more\nthan 10 letters" };
    string vowels = "AEIOU";
    string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    string[] directions = new string[] { "top", "right", "bottom", "left" };
    int[] numbers = Enumerable.Range(0, 8).ToArray();

    int[] symbolIndices = new int[4];
    int[] stringIndices = new int[4];
    bool[] showingBack = new bool[4];
    bool[] isAnimating = new bool[4];

    bool isFighting;
    int fighter, opponent, stage;

    private Coroutine timer;

    private bool lightsAreOn = true;
    public bool TwitchPlaysActive;
    float timeLimit, warning;
    const float TPTime = 30;
    const float TPWarning = 8;
    float flipSpeed = 12;

    #region Modsettings
    class AnomiaSettings
    {
        public float timer = 10;
        public float warningTime = 3;
    }
    AnomiaSettings settings = new AnomiaSettings();
    private static Dictionary<string, object>[] TweaksEditorSettings = new Dictionary<string, object>[]
    {
          new Dictionary<string, object>
          {
            { "Filename", "AnomiaSettings.json"},
            { "Name", "Anomia" },
            { "Listings", new List<Dictionary<string, object>>
                {
                  new Dictionary<string, object>
                  {
                    { "Key", "timer" },
                    { "Text", "Length of the module's timer."}
                  },
                  new Dictionary<string, object>
                  {
                    { "Key", "warningTime" },
                    { "Text", "Time until the module will play a warning tone."}
                  }
                }
            }
          }
    };
    #endregion

    void Awake ()
    {
        //Removes all null entries from the allModules array, in the case that an icon gets deleted.
        allModules = allModules.Where(x => x != null).ToArray();
        moduleId = moduleIdCounter++;
        for (int i = 0; i < 4; i++)
        {
            int ix = i;
            iconButtons[ix].OnInteract += delegate () { IconPress(ix); return false; };
        }
        nextButton.OnInteract += delegate () { Next(); return false; };
        
        //Changes the timers if TP is active.
        Module.OnActivate += delegate () 
        {
            if (TwitchPlaysActive)
            {
                timeLimit = TPTime;
                warning = TPWarning;
            }
        };

        //Connect into when the room's lights change and sets a variable accordingly.
        GameInfo.OnLightsChange += delegate (bool state) {
            if (lightsAreOn != state)
            {
                if (!state)
                    Debug.LogFormat("[Anomia #{0}] Because the lights have turned off, the timer is now paused.", moduleId);
                else
                    Debug.LogFormat("[Anomia #{0}] Lights have turned back on, timer resumed.", moduleId);
                lightsAreOn = state;
            }
        };
        //Shuffles an array of numbers 0-7. These will be used for the initial generation such that no fights happen immediately.0
        numbers.Shuffle();

        //Sets up the modsettings.
        ModConfig<AnomiaSettings> config = new ModConfig<AnomiaSettings>("AnomiaSettings");
        settings = config.Read();
        config.Write(settings);
        timeLimit = settings.timer <= 0 ? 10 : settings.timer; //The timelimit cannot be <= 0.
        warning = settings.warningTime <= 0 || settings.warningTime > timeLimit ? 0.3f * timeLimit : settings.warningTime; //The warning time cannot be <= 0, and it cannot be longer than the time limit.
    }
    
    void Start()
    {
        for (int i = 0; i < 4; i++)
            StartCoroutine(FlipCard(i));//Flips all cards over to their initial states.
        Debug.LogFormat("[Anomia #{0}] INITIAL DISPLAY", moduleId);
        Debug.LogFormat("[Anomia #{0}] The initial cards have symbols {1}.", moduleId, symbolIndices.Select(x => allSymbols[x].name).Join(", "));
        Debug.LogFormat("[Anomia #{0}] The initial messages are {1}.", moduleId, allTexts.Select(x => x.Replace('\n', ' ')).Join(", "));
    }

    void IconPress(int pos)
    {
        iconButtons[pos].AddInteractionPunch(0.3f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, iconButtons[pos].transform);

        //Turns off the timer.
        if (timer != null)
            StopCoroutine(timer);

        //If the module is solved, or any card is in the process of flipping over, return now.
        if (moduleSolved || isAnimating.Any(x => x)) 
            return;
        //If there's no match going on, nothing is correct. Strike anyways.
        if (!isFighting) 
        {
            Module.HandleStrike();
            Debug.LogFormat("[Anomia #{0}] Attempted to press a module while no match was active. Strike incurred.", moduleId);
        }
        //Otherwise, we *are* fighting, so check the validity of the pressed icon against the opponent's card. 
        else if (CheckValidity(opponent, SpriteNames(pos)))
        {
            //As far as we know, the fight is probably over. If another occurs, we'll turn this back on in 2 lines.
            isFighting = false;
            //Flip over the opponent's card, generating a new icon and symbol.
            StartCoroutine(FlipCard(opponent));
            //Check if there's a duplicate. If there is, we're going to start another fight, and thus set isFighting back to true.
            CheckFights();
            //Put icons back on the buttons.
            GenerateIcons();
        }
        else 
        {
            Module.HandleStrike();
            //Stop the timer. Again, if we start another fight, this'll get restarted.
            StopCoroutine(timer);
            Debug.LogFormat("[Anomia #{0}] Attempted to choose a module which did not fit the category. Strike incurred.", moduleId);
            //Flip over *our* card. Then check the duplicates and but the icons back on.
            StartCoroutine(FlipCard(fighter));
            CheckFights();
            GenerateIcons();
        }
    }
    void Next()
    {
        nextButton.AddInteractionPunch(1);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, nextButton.transform);
        //If the module is solved, or any card is flipping over, return here.
        if (moduleSolved || isAnimating.Any(x => x)) 
            return;
        //If we're not in the middle of a fight, we can move onto the next stage.
        else if (!isFighting)
        {
            //If we're on the 12th stage, the module will solve now
            if (stage == 12) 
                StartCoroutine(Solve());
            else
            {
                //Note that the current stage % 4 is the player who the defuser will be controlling.
                //Flip over the new card (generates a new symbol and rule)
                StartCoroutine(FlipCard(stage % 4));
                //Sets the new background to blue.
                backings[stage % 4].GetComponent<MeshRenderer>().material = backingColors[1];
                //Sets the background of the previous player back to gray.
                backings[(stage + 3) % 4].GetComponent<MeshRenderer>().material = backingColors[0];
                stage++;
                Debug.LogFormat("[Anomia #{0}] ::STAGE {1}::", moduleId, stage); 
                //Checks if the new flipped over card causes duplicates.
                CheckFights();
                GenerateIcons();
            }
        }
        else
        {
            Module.HandleStrike();
            Debug.LogFormat("[Anomia #{0}] Arrow button pressed while a match was happening. Strike incurred.", moduleId);
        }
    }

    void GenerateIcons()
    {
        //Shuffles the array of every sprite. 
        allModules.Shuffle();
        Sprite[] tempsprites = new Sprite[4];
        //Fills the first 3 entries of the array with random modules.
        for (int i = 0; i < 3; i++) 
            tempsprites[i] = allModules[i];
        //If we are in a fight, make sure that the 4th entry is a valid module. Otherwise, just pick a module at random.
        if (isFighting)
            tempsprites[3] = allModules.Where(x => x != null && CheckValidity(opponent, x.name.Replace('_', '’'))).PickRandom();
        else tempsprites[3] = allModules[3]; 
        //Shuffle this new array so that the guaranteed valid module is at a random place.
        tempsprites.Shuffle();
        for (int i = 0; i < 4; i++)
            sprites[i].sprite = tempsprites[i];

        //If we get a fight, log the valid modules.
        if (isFighting) 
        {
            List<string> validNames = new List<string>();
            //Takes the names of the icons and checks their validity. If they are valid, add them to the list.
            for (int i = 0; i < 4; i++)
                if (CheckValidity(opponent, SpriteNames(i)))
                        validNames.Add(SpriteNames(i));
            Debug.LogFormat("[Anomia #{0}] The displayed module names are {1}, {2}, {3}, and {4}", moduleId, SpriteNames(0), SpriteNames(1), SpriteNames(2), SpriteNames(3));
            Debug.LogFormat("[Anomia #{0}] The correct possible modules are {1}", moduleId, validNames.Join(", "));
        }
    }

    void CheckFights()
    {
        //If there are not 4 unique symbols, i.e. there's a duplicate, enter a fight.
        if (symbolIndices.Distinct().Count() != 4)
        {
            isFighting = true;
            timer = StartCoroutine(Timer());
            //Gets the symbol which appears twice.
            int duplicate = symbolIndices.Where(x => symbolIndices.Count(y => y == x) == 2).First(); 
            //Sets the player who the defuser will play as.
            for (int i = 0; i < 4; i++)
            {
                //Requires that +3 or else the clockwise movement will start one ahead of where it should.
                int num = (i + stage + 3) % 4;
                if (symbolIndices[num] == duplicate) 
                { 
                    fighter = num; 
                    break; 
                }
            }
            //Sets the player who the defuser will go up against. This is the card whose rule we're using.
            for (int i = 0; i < 4; i++)
            {
                int num = (i + stage + 3) % 4;
                //The fighter and opponent cannot be the same, so skip over the fighter.
                if (num != fighter && symbolIndices[num] == duplicate) 
                { 
                    opponent = num; 
                    break; 
                }
            }
            Debug.LogFormat("[Anomia #{0}] A match has started between the {1} player and the {2} player.", moduleId, directions[fighter], directions[opponent]);
        }
        else isFighting = false;
    }

    bool CheckValidity(int cardNum, string input)
    {
        string name = input.ToUpperInvariant();
        //Looks at the rule of the entered card.
        switch (stringIndices[cardNum])
        {
            case 0: return vowels.Contains(name.First()); //First letter is vowel
            case 1: return vowels.Contains(name.Last()); //Last letter is vowel
            case 2: return name.Contains(' '); //Contains a space
            case 3: return name.Last() == 'S'; //Last letter is S
            case 4: return !name.Contains('E'); //Does not contain an E
            case 5: return !name.Contains(' '); //Contains no spaces.
            case 6: return "ABCDEFGHIJKLM".Contains(name.First()); //First letter is A-M
            case 7: return "ABCDEFGHIJKLM".Contains(name.Last()); //Last letter is A-M
            case 8: return name.Count(x => char.IsLetter(x)) < 8; //Name is <8 letters long.    Requires filtering by only letters or else spaces and other stuff will count.
            case 9: return name.Count(x => char.IsLetter(x)) > 10; //Name is >10 letters long.  
            default: throw new ArgumentOutOfRangeException("stringIndices[cardNum]");
        }
    }

    IEnumerator Solve()
    {
        moduleSolved = true;
        for (int i = 0; i < 4; i++)
        {
            //Flips each card over.
            StartCoroutine(FlipCard(i));
            //Gets an index, which refers to either the front (i) or back (i + 4) of the card.
            int cardFace = showingBack[i] ? i + 4 : i;
            //Clears the text and symbol of the chosen face..
            texts[cardFace].text = string.Empty;
            symbols[cardFace].sprite = null;
            //Unrelated to the cards; clears the icon buttons.
            sprites[i].sprite = null;
        }
        yield return new WaitForSeconds(0.5f);
        Module.HandlePass();
        Audio.PlaySoundAtTransform("Solve", transform);
        //Turns each backing green.
        for (int i = 0; i < 4; i++)
            backings[i].GetComponent<MeshRenderer>().material = backingColors[2];
    }

    IEnumerator FlipCard(int pos)
    {
        if (isAnimating[pos]) 
            yield break;
        isAnimating[pos] = true;
        Audio.PlaySoundAtTransform("Flip", cards[pos].transform);
        //Retrieves an index of the card face. Faces are in order UP-FRONT, RIGHT-FRONT, DOWN-FRONT, LEFT-FRONT, UP-BACK, RIGHT-BACK, DOWN-BACK, LEFT-BACK
        //If the card is currently showing its back, we should change the front, and vice versa.
        int faceAffected = showingBack[pos] ? pos : pos + 4;
        //If we are on the initial stage, use the values from `numbers`, which contains no duplicates. Otherwise, just use a random number.
        symbolIndices[pos] = (stage == 0) ? numbers[pos] : UnityEngine.Random.Range(0, 8);
        //Sets the rule of the card to a random one.
        stringIndices[pos] = UnityEngine.Random.Range(0, allTexts.Length);

        //Sets the text and symbol of the card to the rule & symbol.
        texts[faceAffected].text = allTexts[stringIndices[pos]];
        symbols[faceAffected].sprite = allSymbols[symbolIndices[pos]];

        if (stage != 0 && !moduleSolved) 
            Debug.LogFormat("[Anomia #{0}] The {1} card flipped over. Its symbol is {2} and its message is {3}", 
                moduleId, directions[pos], allSymbols[symbolIndices[pos]].name, allTexts[stringIndices[pos]].Replace('\n',' '));

        isAnimating[pos] = true;
        showingBack[pos] = !showingBack[pos];
        Transform TF = cards[pos].transform;

        //Sets up variables. rotation variables depend on which face is being shown.
        Vector3 startRot = (showingBack[pos] ? 360 : 180) * Vector3.up;
        Vector3 endRot =   (showingBack[pos] ? 180 : 0) * Vector3.up;
        Vector3 startPos = TF.localPosition;
        //height of the card
        const float endpos = -6;

        //Length of the flip animation
        const float duration = 0.75f;
        //Equal to the process along the animation between 0-1
        float delta = 0;
        while (delta < 1)
        {
            delta += Time.deltaTime / duration;
            TF.localPosition = new Vector3(startPos.x, startPos.y, InOutLerp(startPos.z, endpos, delta));
            TF.localEulerAngles = Vector3.Lerp(startRot, endRot, delta);
            yield return null;
        }
        isAnimating[pos] = false;
    }
    //Lerp method that goes from 0-1 then 1-0. If the graph of Lerp is f(x)= x, then this is f(x)= -|2x-1|+1
    private float InOutLerp(float start, float end, float t)
    {
        if (t < 0)
            t = 0;
        if (t > 1)
            t = 1;
        if (t <= 0.5)
            return Mathf.Lerp(start, end, t * 2);
        else return Mathf.Lerp(end, start, t * 2 - 1);
    }
    IEnumerator Timer()
    {
        float currentTime = 0f;
        //Used to determine if we've made the warning noise yet.
        bool playedWarning = false;
        while (true)
        {
            //If the lights are off, don't progress the timer.
            if (lightsAreOn)
                currentTime += Time.deltaTime;
            if (currentTime > timeLimit - warning && !playedWarning)
            {
                Audio.PlaySoundAtTransform("Warning", transform);
                playedWarning = true;
            }
            //If the elapsed time has past the time limit, we've ran out of time.
            if (currentTime > timeLimit)
            {
                StopCoroutine(timer);
                Module.HandleStrike();
                Debug.LogFormat("[Anomia #{0}] Timer expired, strike incurred.", moduleId);
                StartCoroutine(FlipCard(fighter));
                CheckFights();
                GenerateIcons();
            }
            yield return null;
        }
    }
    //Gets the name of the icon on a certain icon button.
    string SpriteNames(int pos)
    {
        return sprites[pos].sprite.name.Replace('_', '’');
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use !{0} press 1/2/3/4 to press the button in that position in reading order. Use {0} next to press the arrow buton. On TP, the timer is extended to 30 seconds.";
    #pragma warning restore 414
    IEnumerator Press(KMSelectable btn, float delay)
    {
        btn.OnInteract();
        yield return new WaitForSeconds(delay);
    }
    IEnumerator ProcessTwitchCommand (string input)
    {
        input = input.Trim().ToUpperInvariant();
        List<string> parameters = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        string[] possibleCommands = { "1", "2", "3", "4", "TL", "TR", "BL", "BR" };
        if (Regex.IsMatch(input, @"^(PRESS)?\s*([1-4]|([TB][LR]))$"))
        {
            yield return null;
            yield return Press(iconButtons[Array.IndexOf(possibleCommands, parameters.Last()) % 4], 0.1f);
        }
        else if (input == "NEXT")
        {
            yield return null;
            yield return Press(nextButton, 0.1f);
        }
    }

    IEnumerator TwitchHandleForcedSolve ()
    {
        while (!moduleSolved)
        {
            if (!isFighting)
            {
                while (isAnimating.Any(x => x)) 
                    yield return true;
                yield return Press(nextButton, 0.1f);
            }
            else
                //Presses the first icon button which has a valid icon.
                yield return Press(iconButtons[Enumerable.Range(0, 4).First(num => CheckValidity(opponent, SpriteNames(num)))], 0.1f);
            yield return null;
        }
    }
}
