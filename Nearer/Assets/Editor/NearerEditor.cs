using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;
using UnityEngine.TextCore.LowLevel;
using TextAsset = UnityEngine.TextAsset;

namespace Nearer.Editor
{
    [InitializeOnLoad]
    public static class NearerEditor
    {
        const string ScenePath = "Assets/Scenes/Nearer.unity";
        static readonly string Inbox = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".nearer-local", "editor"));
        static double nextPoll;
        static bool buildQueued;

        static NearerEditor()
        {
            Directory.CreateDirectory(Inbox);
            EditorApplication.update += Poll;
        }

        [MenuItem("Nearer/Prepare project")]
        public static void Prepare()
        {
            PrepareTypography();
            PlayerSettings.companyName = "Nearer";
            PlayerSettings.productName = "Nearer";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "game.nearer.daily");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "game.nearer.daily");
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "PROJECT:Nearer";
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Nearer").AddComponent<NearerApp>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene(ScenePath, true)};
            AssetDatabase.SaveAssets();
            Debug.Log("Nearer project prepared.");
        }

        static void PrepareTypography()
        {
            string fontPath="Assets/Resources/Fonts/NearerSans.asset";
            var font=AssetDatabase.LoadAssetAtPath<FontAsset>(fontPath);
            if(font==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/LiberationSans-Regular.ttf");
                font=FontAsset.CreateFontAsset(source,64,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
                font.name="Nearer Sans";
                font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?'-()&+%\u2212");
                AssetDatabase.CreateAsset(font,fontPath);
                font.material.name="Nearer Sans Material";
                AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var atlas in font.atlasTextures)if(atlas!=null)AssetDatabase.AddObjectToAsset(atlas,font);
                EditorUtility.SetDirty(font);
            }
            string panelPath="Assets/Resources/NearerPanel.asset";
            var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if(panel==null){panel=ScriptableObject.CreateInstance<PanelSettings>();AssetDatabase.CreateAsset(panel,panelPath);}
            panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Resources/NearerTheme.tss");
            if(panel.textSettings==null)
            {
                var text=ScriptableObject.CreateInstance<PanelTextSettings>();
                text.defaultFontAsset=font;
                AssetDatabase.CreateAsset(text,"Assets/Resources/NearerText.asset");
                panel.textSettings=text;
            }
            panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            panel.textureSlotCount=TextureSlotCount.One;
            EditorUtility.SetDirty(panel);
        }

        [MenuItem("Nearer/Verify rules and data")]
        public static void Verify()
        {
            var bank = JsonUtility.FromJson<PuzzleBank>(Resources.Load<TextAsset>("nearer-puzzles").text);
            Check(bank.puzzles.Length >= 7, "Seven daily boards");
            int pairs = 0;
            foreach (var puzzle in bank.puzzles)
            {
                Check(puzzle.topics.Length == 5, "Five topics");
                Check(puzzle.anchorRevision > 0, "Anchor revision");
                var state = Rules.NewRound(puzzle);
                Check(Rules.Valid(state, puzzle), "New state validity");
                Check(!Rules.Complete(state), "New round tracks untouched topics");
                state.revealed=true;
                Check(Rules.Valid(state,puzzle),"An untouched revealed round can resume");
                state.revealed=false;
                for (int i = 0; i < 5; i++)
                {
                    var topic = puzzle.topics[i];
                    Check(topic.revision > 0 && topic.evidence.Length > 0, "Source evidence");
                    Check(topic.unionCount == puzzle.anchorLinkCount + topic.linkCount - topic.sharedCount, "Set union");
                    Check(Mathf.Abs(topic.similarity - (float)topic.sharedCount / topic.unionCount) < 0.00001f, "Jaccard calculation");
                    state.positions[i] = Vector2.right * Rules.ReferenceRadius(puzzle, i);
                    state.placed[i] = true;
                }
                Check(Rules.Complete(state), "All five placed");
                var correct = Rules.Score(puzzle, state);
                pairs += correct.total;
                Check(correct.percent == 100, "Correct order scores 100");
                VerifyTopicPoints(puzzle,state);
                Check(correct.total == puzzle.informativePairs && correct.total >= 6, "Informative pair count");
                for (int i = 0; i < 5; i++) state.positions[i] = Vector2.right * (1.12f - Rules.ReferenceRadius(puzzle, i));
                Check(Rules.Score(puzzle, state).percent == 0, "Reverse order scores zero");
                VerifyTopicPoints(puzzle,state);
                for (int i = 0; i < 5; i++) state.positions[i] = Vector2.right * 0.5f;
                Check(Rules.Score(puzzle, state).percent == 0, "Equal radii cannot bypass scoring");
                VerifyTopicPoints(puzzle,state);
                state.revealed = true;
                var roundTrip = JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(state));
                Check(Rules.Valid(roundTrip, puzzle) && roundTrip.revealed, "Completed progress roundtrip");
                roundTrip.positions[0].x = float.NaN;
                Check(!Rules.Valid(roundTrip, puzzle), "Reject corrupt coordinates");
            }
            var untouchedPuzzle=new Puzzle{id="untouched-test",tieTolerance=0,topics=new[]{new Topic{similarity=1},new Topic{similarity=.8f},new Topic{similarity=.6f},new Topic{similarity=.4f},new Topic{similarity=.2f}}};
            var untouched=Rules.NewRound(untouchedPuzzle);
            for(int i=0;i<4;i++){untouched.positions[i]=untouched.positions[i].normalized*(.3f+i*.15f);untouched.placed[i]=true;}
            Check(!untouched.placed[4]&&Rules.Score(untouchedPuzzle,untouched).percent==100&&Rules.TopicPoints(untouchedPuzzle,untouched)[4]==20,"An untouched circle earns its full share when its distance is correctly ordered");
            var beforeMarking=Rules.Score(untouchedPuzzle,untouched);var beforeAwards=Rules.TopicPoints(untouchedPuzzle,untouched);
            for(int i=0;i<5;i++)untouched.placed[i]=false;
            Check(Rules.Score(untouchedPuzzle,untouched).percent==beforeMarking.percent&&string.Join(",",Rules.TopicPoints(untouchedPuzzle,untouched))==string.Join(",",beforeAwards),"Movement flags never change original comparison scoring or awards");
            Debug.Log("NEARDLE UNTOUCHED PASS: untouched circles earn correct comparison points; movement flags never affect scoring.");
            var p = bank.puzzles[0];
            var tiePuzzle = new Puzzle {id = "ties", tieTolerance = 0.01f, topics = new[] {new Topic {similarity = 0.2f}, new Topic {similarity = 0.205f}, new Topic {similarity = 0.1f}}};
            var tieState = new RoundState {positions = new[] {Vector2.right * 0.3f, Vector2.right * 0.2f, Vector2.right * 0.8f}, placed = new[] {true, true, true}};
            Check(Rules.Score(tiePuzzle, tieState).percent == 100 && Rules.Score(tiePuzzle, tieState).total == 2, "Near ties accept either order");
            VerifyTopicPoints(tiePuzzle,tieState);
            var allTied=new Puzzle{tieTolerance=0.01f,topics=new[]{new Topic{similarity=0.2f},new Topic{similarity=0.2f},new Topic{similarity=0.2f}}};
            VerifyTopicPoints(allTied,tieState);
            var clockwise=Rules.ClockwiseOrder(new[]{Vector2.left,Vector2.up,Vector2.down,Vector2.right,new Vector2(1,-1)});
            Check(string.Join(",",clockwise)=="2,4,3,1,0","Awards run clockwise from twelve o'clock in screen coordinates");
            var sameAngle=Rules.ClockwiseOrder(new[]{Vector2.down,Vector2.down*0.5f,Vector2.right});
            Check(string.Join(",",sameAngle)=="0,1,2","Clockwise ties are deterministic");
            var pathFixture=new SourceCatalog{articles=new[]{
                new SourceArticle{title="A",links=new[]{"C","B"}},
                new SourceArticle{title="B",links=new[]{"D","A"}},
                new SourceArticle{title="C",links=new[]{"E"}},
                new SourceArticle{title="E",links=new[]{"D"}},
                new SourceArticle{title="D",links=Array.Empty<string>()},
                new SourceArticle{title="Isolated",links=new[]{"D"}}}};
            Check(string.Join(",",QuestionStream.FindShortestPath(pathFixture,"A","D"))=="A,B,D","BFS returns the shortest directed route, not the first depth-first route");
            Check(QuestionStream.FindShortestPath(pathFixture,"D","A").Length==0,"Paths never follow links backwards");
            Check(QuestionStream.FindShortestPath(pathFixture,"A","Isolated").Length==0,"Unreachable pages cannot be connected through a shared outgoing link");
            Check(QuestionStream.FindShortestPath(pathFixture,"A","A").Length==1,"Identity route has zero links");
            Check(Rules.DayIndex(new DateTime(2026, 10, 18, 23, 59, 59), bank.epoch, 7) == 0, "UTC day start");
            Check(Rules.DayIndex(new DateTime(2026, 10, 19), bank.epoch, 7) == 1, "UTC next day");
            Check(Rules.DayIndex(new DateTime(2026, 10, 25), bank.epoch, 7) == 0, "Seven-day cycle");
            Check(Rules.DayIndex(new DateTime(2026, 10, 17), bank.epoch, 7) == 6, "Dates before epoch");
            var catalog=JsonUtility.FromJson<SourceCatalog>(Resources.Load<TextAsset>("nearer-catalog").text);
            var stream=new QuestionStream(catalog);
            Check(stream.CollectionCount>=6,"Six editorial collections");
            var signatures=new System.Collections.Generic.HashSet<string>();
            var categories=new System.Collections.Generic.HashSet<string>();
            var samples=new System.Collections.Generic.List<Puzzle>();
            int wildcardCount=0;
            for(int i=0;i<1000;i++)
            {
                var generated=stream.Generate("2026-10-18",i);
                if(i<12)samples.Add(generated);
                Check(generated.topics.Length==5&&generated.informativePairs>=6,"Generated question quality");
                var anchorArticle=Array.Find(catalog.articles,a=>a.title==generated.anchorTitle);
                int related=0;
                foreach(var topic in generated.topics)if(QuestionStream.Related(anchorArticle,Array.Find(catalog.articles,a=>a.title==topic.title)))related++;
                Check(related>=4,"At least four contextually related choices");
                if(related<5)wildcardCount++;
                if(i%stream.CollectionCount==0)categories.Clear();
                Check(categories.Add(generated.category),"Collection variety before repeating an area");
                string signature=generated.anchorTitle;
                foreach(var topic in generated.topics)signature+="|"+topic.title;
                signatures.Add(signature);
                var generatedState=Rules.NewRound(generated);
                for(int j=0;j<5;j++){generatedState.positions[j]=Vector2.right*Rules.ReferenceRadius(generated,j);generatedState.placed[j]=true;}
                Check(Rules.Score(generated,generatedState).percent==100,"Generated reference score");
                if(i<12)foreach(var topic in generated.topics)
                {
                    var path=stream.ShortestPath(generated.anchorTitle,topic.title);
                    if(path.Length==0)continue;
                    Check(path[0]==generated.anchorTitle&&path[path.Length-1]==topic.title,"Paths connect the requested articles");
                    for(int step=1;step<path.Length;step++)Check(stream.HasLink(path[step-1],path[step]),"Every path step is a directed Wikipedia link");
                }
                VerifyTopicPoints(generated,generatedState);
                // Mixed orders exercise fractional point rounding, beyond perfect/zero scores.
                for(int j=0;j<5;j++)generatedState.positions[j]=Vector2.right*Mathf.Lerp(0.2f,0.95f,((i*7+j*3)%11)/10f);
                VerifyTopicPoints(generated,generatedState);
                Check(stream.Generate("2026-10-18",i).id==generated.id,"Reproducible generated question");
            }
            Check(signatures.Count>=900,"Procedural question variety");
            File.WriteAllText(Path.Combine(Inbox,"sample-questions.json"),JsonUtility.ToJson(new PuzzleBank{puzzles=samples.ToArray()},true));
            var wanted=new Vector2[5];var boxes=new Vector2[5];
            for(int i=0;i<5;i++){wanted[i]=Vector2.right*170;boxes[i]=new Vector2(140,88);}
            var arranged=MagnetLayout.Arrange(wanted,boxes,new Rect(-600,-280,1200,560),new Rect(-72,-70,144,132),0);
            for(int i=0;i<5;i++)
            {
                Check(Mathf.Abs(arranged[i].magnitude-wanted[i].magnitude)<0.001f,"Collision layout preserves distance");
                for(int j=i+1;j<5;j++)Check(MagnetLayout.Overlap(new Rect(arranged[i]-boxes[i]*0.5f,boxes[i]),new Rect(arranged[j]-boxes[j]*0.5f,boxes[j]))<1,"Clustered magnets do not overlap");
            }
            var rankPuzzle=new Puzzle{id="rank-test",rankBoxes=true,tieTolerance=0,topics=new[]{new Topic{similarity=1},new Topic{similarity=.8f},new Topic{similarity=.6f},new Topic{similarity=.4f},new Topic{similarity=.2f}}};
            var rankState=Rules.NewRound(rankPuzzle);
            Check(Rules.SetRanking(rankState,new[]{0,1,2,3,4}),"Rank placements accepted");
            Check(Rules.Score(rankPuzzle,rankState).percent==100,"Five correct boxes earn 100");VerifyTopicPoints(rankPuzzle,rankState);
            Check(Rules.SetRanking(rankState,new[]{1,0,2,3,4})&&Rules.Score(rankPuzzle,rankState).percent==80,"Two adjacent swapped boxes earn 10 each, for 80 total");
            string beforeInvalid=JsonUtility.ToJson(rankState);
            Check(!Rules.SetRanking(rankState,new[]{0,0,2,3,4})&&JsonUtility.ToJson(rankState)==beforeInvalid,"Duplicate box is rejected atomically");
            Check(!Rules.SetRanking(rankState,new[]{0,1,2,3,5})&&!Rules.SetRanking(rankState,new[]{0,1}),"Invalid ranking payloads rejected");
            Check(Rules.SetRanking(rankState,new[]{4,3,2,1,0})&&Rules.Score(rankPuzzle,rankState).percent==20,"Reverse ordering only scores the middle box");VerifyTopicPoints(rankPuzzle,rankState);
            Check(Rules.SetRanking(rankState,new[]{1,2,3,4,0})&&Rules.Score(rankPuzzle,rankState).percent==40&&Rules.Score(rankPuzzle,rankState).correct==0,"Four adjacent placements earn 40 without counting as exact");VerifyTopicPoints(rankPuzzle,rankState);
            rankPuzzle.topics[1].similarity=.99f;rankPuzzle.tieTolerance=.02f;
            Check(Rules.SetRanking(rankState,new[]{1,0,2,3,4})&&Rules.Score(rankPuzzle,rankState).percent==100,"Near ties accept either box order");
            Check(Rules.SetRanking(rankState,new[]{-1,0,2,3,4})&&!Rules.Complete(rankState)&&Rules.Score(rankPuzzle,rankState).percent==80,"Tray topics remain incomplete and earn no points");
            rankState.revealed=true;Check(!Rules.SetRanking(rankState,new[]{0,1,2,3,4}),"Revealed rankings are immutable");
            Debug.Log("NEARDLE BOX PASS: exact and adjacent placement scoring, swaps, ties, partial rankings, invalid payloads and reveal locking.");
            Debug.Log("NEARER STREAM PASS: "+stream.ArticleCount+" articles, "+stream.AnchorCount+" centers, "+stream.CollectionCount+" collections, "+signatures.Count+" distinct questions from 1000, "+wildcardCount+" supported wildcards, reproducible generation and crowded magnet layout.");
            Debug.Log("NEARER PASS: seven boards, " + pairs + " comparisons, scoring, ties, equal radii, persistence, corrupt saves and UTC dates.");
        }

        static void VerifyTopicPoints(Puzzle puzzle,RoundState state)
        {
            int total=0;var awards=Rules.TopicPoints(puzzle,state);
            Check(awards.Length==puzzle.topics.Length,"One award per topic");
            foreach(int award in awards){Check(award>=0,"Topic awards cannot be negative");total+=award;}
            Check(total==Rules.Score(puzzle,state).percent,"Topic awards add up to the round score");
        }

        static void Check(bool value, string message)
        {
            if (!value) throw new Exception("Nearer verification failed: " + message);
        }

        static void FocusGame()
        {
            var gameType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(gameType);
            var size=gameType.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            size?.SetValue(window,0);
            window.Focus();
            window.maximized = true;
        }

        [MenuItem("Nearer/Connect editor")]
        public static async void Connect()
        {
            try
            {
                if (!MCPForUnity.Editor.Services.MCPServiceLocator.Server.StartLocalHttpServer(true))
                {
                    Debug.LogWarning("Nearer editor connection could not start. Open the MCP for Unity window to review its settings.");
                    return;
                }
                await System.Threading.Tasks.Task.Delay(1800);
                bool connected = await MCPForUnity.Editor.Services.MCPServiceLocator.Bridge.StartAsync();
                Debug.Log(connected ? "Nearer editor connected." : "Nearer editor connection is waiting for the local server.");
            }
            catch (Exception error) {Debug.LogException(error);}
        }

        [MenuItem("Nearer/Build browser game")]
        public static void BuildWeb()
        {
            // MCP can dispatch menu actions from Unity's synchronization context.
            // BuildPipeline must run on a subsequent editor tick, outside that loop.
            buildQueued=true;
            Debug.Log("Nearer browser build queued.");
        }

        static void BuildWeb(bool verifyData)
        {
            Prepare();
            if(verifyData)Verify();
            Directory.CreateDirectory("Builds/Web");
            var options = new BuildPlayerOptions
            {
                scenes = new[] {ScenePath},
                locationPathName = "Builds/Web",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Nearer browser build failed: " + report.summary.result);
            ExportBrowserMedia();
            Debug.Log("NEARER BUILD PASS: " + report.summary.totalSize + " bytes.");
        }

        static void ExportBrowserMedia()
        {
            var media=JsonUtility.FromJson<MediaBank>(Resources.Load<TextAsset>("nearer-media").text);
            Directory.CreateDirectory("Builds/Web/Images");
            foreach(var credit in media.entries)
            {
                string stem=Path.GetFileName(credit.resource);
                foreach(string file in Directory.GetFiles("Assets/Resources/Images",stem+".*"))
                {
                    if(file.EndsWith(".meta",StringComparison.OrdinalIgnoreCase))continue;
                    credit.browserImage="Images/"+Path.GetFileName(file);
                    File.Copy(file,Path.Combine("Builds/Web",credit.browserImage),true);break;
                }
                if(string.IsNullOrEmpty(credit.browserImage))throw new Exception("Missing browser portrait: "+credit.title);
            }
            File.WriteAllText("Builds/Web/media.json",JsonUtility.ToJson(media));
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.25;
            if(buildQueued)
            {
                buildQueued=false;
                if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogError("Stop Play Mode before building Nearer.");return;}
                BuildWeb(true);
                return;
            }
            string path = Path.Combine(Inbox, "command.json");
            if (!File.Exists(path)) return;
            string result;
            try
            {
                var command = JsonUtility.FromJson<Command>(File.ReadAllText(path));
                File.Delete(path);
                switch (command.action)
                {
                    case "status": result = "playing=" + EditorApplication.isPlaying + "; compiling=" + EditorApplication.isCompiling; break;
                    case "clear":
                        typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public).Invoke(null,null);
                        result="console cleared";break;
                    case "console":
                        var counts=new object[]{0,0,0};
                        typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public).Invoke(null,counts);
                        result="errors="+counts[0]+"; warnings="+counts[1]+"; logs="+counts[2];break;
                    case "refresh": AssetDatabase.Refresh(); result = "refreshed"; break;
                    case "connect": Connect(); result = "connecting"; break;
                    case "prepare": Prepare(); result = "prepared"; break;
                    case "verify": Verify(); result = "verified"; break;
                    case "play":
                        EditorSceneManager.OpenScene(ScenePath);
                        FocusGame();
                        EditorApplication.isPlaying = true;
                        result = "entering play"; break;
                    case "stop": EditorApplication.isPlaying = false; result = "stopped"; break;
                    case "snapshot": result = NearerApp.Instance == null ? "not playing" : NearerApp.Instance.Snapshot(); break;
                    case "place": NearerApp.Instance.PlaceNormalized(command.index, new Vector2(command.x, command.y)); result = NearerApp.Instance.Snapshot(); break;
                    case "reveal": NearerApp.Instance.Reveal(); result = "revealing"; break;
                    case "practice": NearerApp.Instance.OpenPractice(); result = NearerApp.Instance.Snapshot(); break;
                    case "next": NearerApp.Instance.Next(); result = NearerApp.Instance.Snapshot(); break;
                    case "capture":
                        FocusGame();
                        string capture = Path.Combine(Inbox, "game.png");
                        ScreenCapture.CaptureScreenshot(capture);
                        result = "capture requested"; break;
                    case "build": BuildWeb(); result = "built"; break;
                    case "build-preview": BuildWeb(false); result = "preview built"; break;
                    default: result = "ready"; break;
                }
                File.WriteAllText(Path.Combine(Inbox, "result.json"), JsonUtility.ToJson(new Result {ok = true, message = result}));
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(Inbox, "result.json"), JsonUtility.ToJson(new Result {ok = false, message = ex.Message}));
                Debug.LogException(ex);
            }
        }

        [Serializable] class Command {public string action; public int index; public float x; public float y;}
        [Serializable] class Result {public bool ok; public string message;}
    }
}
