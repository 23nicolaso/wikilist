using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nearer
{
    public sealed class NearerApp : MonoBehaviour
    {
        public static NearerApp Instance {get;private set;}
        public PuzzleBank Bank {get;private set;}
        public Puzzle Puzzle {get;private set;}
        public RoundState State {get;private set;}
        public bool ReducedMotion {get;private set;}
        public bool IsRevealing {get;private set;}
        public string Mode => "stream";
        public int Selected {get;private set;}=-1;
        public string StorageKey {get;private set;}
        QuestionStream stream;
        MediaBank media;
        VisualElement root,app,board,overlay,adjust,evidence,centerGroup,centerPhoto;
        VisualElement[] nodes,photos;
        Label[] pointBadges;
        int[] topicPoints;
        int[] revealOrder;
        int[] laneOrder;
        string[][] connectionPaths;
        readonly bool[] awarded=new bool[5];
        int awardsShown, revealedPoints;
        bool correcting;
        bool browserUI;
        Label anchor,edition,instruction,status,score;
        Button action,share,imagesButton;
        FieldDrawing field;
        PanelSettings panel;
        Texture2D wash;
        readonly Color[] washPixels=new Color[32*32];
        readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        Vector2 center;
        Vector2[] current=new Vector2[5],targets=new Vector2[5],nodeSizes=new Vector2[5],ghosts=new Vector2[5];
        float radius,innerRadius;
        float PhotoOffset=>images&&!compact?(wide?15.5f:13.5f):0;
        bool compact,wide,hasRail,layoutDirty=true,dragging,moved,images=true,targetsReady,initialized;
        int dragPointer,question;
        Vector2 pointerStart,dragOffset;
        string date,notice;
        Palette fromPalette,toPalette,theme;
        float paletteTime=1;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern string NearerRead(string key);
        [DllImport("__Internal")]static extern int NearerWrite(string key,string value);
        [DllImport("__Internal")]static extern int NearerMotionPreference();
        [DllImport("__Internal")]static extern void NearerCopy(string value);
        [DllImport("__Internal")]static extern float NearerPixelRatio();
        [DllImport("__Internal")]static extern void NearerPublishState(string value);
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(FindAnyObjectByType<NearerApp>()==null)new GameObject("Nearer").AddComponent<NearerApp>();}
        void Awake()
        {
            Instance=this;Application.targetFrameRate=60;
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.cullingMask=0;camera.backgroundColor=FieldDrawing.Mineral;
            Bank=JsonUtility.FromJson<PuzzleBank>(Resources.Load<TextAsset>("nearer-puzzles").text);
            stream=new QuestionStream(JsonUtility.FromJson<SourceCatalog>(Resources.Load<TextAsset>("nearer-catalog").text));
            var asset=Resources.Load<TextAsset>("nearer-media");
            media=asset==null?new MediaBank{entries=Array.Empty<MediaCredit>()}:JsonUtility.FromJson<MediaBank>(asset.text);
            ReducedMotion=Read("nearer.motion")=="reduced";
#if UNITY_WEBGL && !UNITY_EDITOR
            if(string.IsNullOrEmpty(Read("nearer.motion")))ReducedMotion=NearerMotionPreference()==1;
#endif
            images=Read("nearer.images")!="off";
            date=DateTime.UtcNow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            int.TryParse(Read("nearer.v3.sequence."+date),out question);question=Mathf.Max(0,question);
#if UNITY_WEBGL && !UNITY_EDITOR
            // The browser owns the visible interface. No UIDocument, image masks,
            // vector meshes or dynamic UI buffers are submitted to WebGL.
            WebGLInput.captureAllKeyboardInput=false;
            camera.enabled=false;browserUI=true;OpenQuestion(false);initialized=true;return;
#else
            panel=Resources.Load<PanelSettings>("NearerPanel");
            if(panel==null)panel=ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
#if UNITY_WEBGL && !UNITY_EDITOR
            panel.scale=Mathf.Max(1,NearerPixelRatio());
#endif
            panel.clearColor=true;
            // Avoid the multi-sampler shader path: on some WebGL drivers portraits
            // alternate with the missing-texture checkerboard when slots are reused.
            panel.textureSlotCount=TextureSlotCount.One;
            // Keep changing atmosphere pixels and cropped portraits out of the same
            // dynamic atlas. Their UVs must stay stable while the field animates.
            var atlas=panel.dynamicAtlasSettings;atlas.customFilter=ExcludeFromAtlas;panel.dynamicAtlasSettings=atlas;
            var doc=gameObject.AddComponent<UIDocument>();doc.panelSettings=panel;
            root=doc.rootVisualElement;root.styleSheets.Add(Resources.Load<StyleSheet>("Nearer"));
            wash=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Nearer atmosphere",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            BuildUI();OpenQuestion(false);initialized=true;
#endif
        }
        void BuildUI()
        {
            app=Element(root,"app");app.style.backgroundImage=wash;
            var top=Element(app,"topbar");Text(top,"neardle","brand");
            var nav=Element(top,"navigation");imagesButton=Button(nav,"Images on",ToggleImages,"quiet");Button(nav,"How to play",ShowHelp,"quiet");
            edition=Text(app,"","edition");instruction=Text(app,"Bring related things closer.","instruction");score=Text(app,"","score");
            board=Element(app,"board");field=new FieldDrawing();field.StretchToParentSize();board.Add(field);
            centerGroup=Element(board,"center-group");centerPhoto=Element(centerGroup,"center-photo");anchor=Text(centerGroup,"","anchor");
            anchor.RegisterCallback<GeometryChangedEvent>(e=>{if(Vector2.Distance(e.newRect.size,e.oldRect.size)>0.5f)layoutDirty=true;});
            centerGroup.pickingMode=PickingMode.Ignore;centerPhoto.pickingMode=PickingMode.Ignore;anchor.pickingMode=PickingMode.Ignore;
            nodes=new VisualElement[5];photos=new VisualElement[5];
            pointBadges=new Label[5];
            for(int i=0;i<5;i++)
            {
                int index=i;var node=Element(board,"topic");node.name="topic-"+i;node.focusable=true;
                node.usageHints=UsageHints.DynamicTransform;
                photos[i]=Element(node,"topic-photo");photos[i].pickingMode=PickingMode.Ignore;
                var label=Text(node,"","topic-label");label.pickingMode=PickingMode.Ignore;
                pointBadges[i]=Text(node,"","topic-points");pointBadges[i].pickingMode=PickingMode.Ignore;pointBadges[i].style.display=DisplayStyle.None;
                node.RegisterCallback<PointerDownEvent>(e=>NodeDown(index,e));node.RegisterCallback<PointerMoveEvent>(e=>NodeMove(index,e));node.RegisterCallback<PointerUpEvent>(e=>NodeUp(index,e));
                node.RegisterCallback<PointerCaptureOutEvent>(e=>{if(dragging){dragging=false;nodes[index].RemoveFromClassList("dragging");Save();}});
                node.RegisterCallback<KeyDownEvent>(e=>NodeKey(index,e));node.RegisterCallback<FocusInEvent>(e=>Select(index));nodes[i]=node;
            }
            board.RegisterCallback<GeometryChangedEvent>(e=>{if(Vector2.Distance(e.newRect.size,e.oldRect.size)>0.5f)layoutDirty=true;});
            board.RegisterCallback<PointerDownEvent>(e=>{if(e.target!=board||Selected<0||overlay!=null||State.revealed)return;Place(Selected,board.WorldToLocal(e.position));e.StopPropagation();});
            adjust=Element(app,"adjust");Button(adjust,"Closer",()=>Nudge(-0.065f),"quiet nudge");Button(adjust,"Farther",()=>Nudge(0.065f),"quiet nudge");
            var dock=Element(app,"dock");action=Button(dock,"Reveal",MainAction,"action");share=Button(dock,"Share",Share,"quiet share");status=Text(app,"","status");
            var footer=Element(app,"footer");Button(footer,"The connections",ShowSources,"quiet foot-button");
            root.RegisterCallback<GeometryChangedEvent>(e=>{if(Vector2.Distance(e.newRect.size,e.oldRect.size)<0.5f)return;bool small=e.newRect.width<700,large=e.newRect.width>1500,rail=e.newRect.width>=1280;if(compact!=small||wide!=large||hasRail!=rail){compact=small;wide=large;hasRail=rail;UpdateNodeSizes();UpdateEvidence();}app.EnableInClassList("compact",compact);app.EnableInClassList("wide",wide);app.EnableInClassList("has-rail",hasRail);layoutDirty=true;});
            root.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Escape){CloseOverlay();if(!State.revealed)Selected=-1;UpdateUI();}});
        }
        static VisualElement Element(VisualElement parent,string classes){var e=new VisualElement();foreach(string c in classes.Split(' '))e.AddToClassList(c);parent.Add(e);return e;}
        static bool ExcludeFromAtlas(Texture2D texture,ref DynamicAtlasFilters filters)=>false;
        static Label Text(VisualElement parent,string value,string classes){var e=new Label(value);foreach(string c in classes.Split(' '))e.AddToClassList(c);parent.Add(e);return e;}
        Button Button(VisualElement parent,string value,Action callback,string classes){var e=new Button(callback){text=value};foreach(string c in classes.Split(' '))e.AddToClassList(c);if(e.ClassListContains("action"))e.style.backgroundColor=theme.Accent;parent.Add(e);return e;}
        void OpenQuestion(bool transition)
        {
            StopAllCoroutines();IsRevealing=false;dragging=false;Selected=-1;notice=null;CloseOverlay();
            Puzzle=stream.Generate(date,question);StorageKey="nearer.v3.round."+date+"."+question;
            connectionPaths=new string[5][];for(int i=0;i<5;i++)connectionPaths[i]=stream.ShortestPath(Puzzle.anchorTitle,Puzzle.topics[i].title);
            RoundState saved=null;try{string json=Read(StorageKey);if(!string.IsNullOrEmpty(json))saved=JsonUtility.FromJson<RoundState>(json);}catch{}
            State=Rules.Valid(saved,Puzzle)?saved:Rules.NewRound(Puzzle);
            topicPoints=Rules.TopicPoints(Puzzle,State);awardsShown=State.revealed?5:0;revealedPoints=State.revealed?Rules.Score(Puzzle,State).percent:0;correcting=State.revealed;
            laneOrder=Rules.ClockwiseOrder(Rules.NewRound(Puzzle).positions);
            // Fresh rounds start every topic at the same midpoint on its ruler.
            if(!State.revealed&&Array.TrueForAll(State.placed,placed=>!placed))
                for(int slot=0;slot<5;slot++)State.positions[laneOrder[slot]]=Vector2.right*Mathf.Lerp(Rules.MinimumRadius,Rules.MaximumRadius,0.5f);
            revealOrder=(int[])laneOrder.Clone();for(int i=0;i<5;i++)awarded[i]=State.revealed;
            if(browserUI)
            {
                theme=Palette.At(question);toPalette=theme;
                Write("nearer.v3.sequence."+date,question.ToString(CultureInfo.InvariantCulture));UpdateUI();return;
            }
            if(State.revealed)Selected=0;
            anchor.text=Puzzle.anchor;edition.text="Map "+(question+1).ToString("00");
            for(int i=0;i<5;i++){nodes[i].Q<Label>(className:"topic-label").text=Puzzle.topics[i].label;SetPhoto(photos[i],Puzzle.topics[i].title);}
            SetPhoto(centerPhoto,Puzzle.anchorTitle);UpdateNodeSizes();targetsReady=false;layoutDirty=true;
            fromPalette=transition?theme:Palette.At(question);toPalette=Palette.At(question);paletteTime=transition&&!ReducedMotion?0:1;
            theme=fromPalette;ApplyTheme();UpdateUI();Write("nearer.v3.sequence."+date,question.ToString(CultureInfo.InvariantCulture));
        }
        public void Next(){if(IsRevealing)return;question++;OpenQuestion(true);}
        public void OpenPractice()=>Next();
        public void OpenDaily(){question=0;OpenQuestion(true);}
        void MainAction(){if(State.revealed)Next();else Reveal();}
        void ToggleImages(){images=!images;Write("nearer.images",images?"on":"off");if(browserUI){UpdateUI();return;}for(int i=0;i<5;i++)SetPhoto(photos[i],Puzzle.topics[i].title);SetPhoto(centerPhoto,Puzzle.anchorTitle);UpdateNodeSizes();UpdateUI();}
        void SetPhoto(VisualElement element,string title)
        {
            MediaCredit credit=Array.Find(media.entries,c=>c.title==title);Texture2D image=null;
            if(credit!=null&&!textures.TryGetValue(credit.resource,out image)){image=Resources.Load<Texture2D>(credit.resource);textures[credit.resource]=image;}
            var portrait=element.Q<Image>("portrait");
            if(portrait==null){portrait=new Image{name="portrait",scaleMode=ScaleMode.ScaleAndCrop,pickingMode=PickingMode.Ignore};portrait.AddToClassList("portrait");element.Insert(0,portrait);}
            portrait.image=image;portrait.style.display=images&&image!=null?DisplayStyle.Flex:DisplayStyle.None;element.EnableInClassList("no-image",image==null);
            var initial=element.Q<Label>("image-initial");if(initial==null){initial=Text(element,"","image-initial");initial.name="image-initial";initial.pickingMode=PickingMode.Ignore;}
            initial.text=title.Substring(0,1).ToUpperInvariant();initial.style.display=images&&image==null?DisplayStyle.Flex:DisplayStyle.None;
        }
        void UpdateNodeSizes()
        {
            if(Puzzle==null)return;app.EnableInClassList("no-photos",!images);
            for(int i=0;i<5;i++){float width=Mathf.Clamp(Puzzle.topics[i].label.Length*(compact?6.8f:wide?9.4f:8.6f)+18,compact?80:112,compact?132:180);float height=images&&!compact?(wide?102:88):46;nodeSizes[i]=new Vector2(width,height);nodes[i].style.width=width;nodes[i].style.height=height;}layoutDirty=true;
        }
        void Update()
        {
            if(browserUI||!initialized||State==null)return;if(layoutDirty){layoutDirty=false;SolveLayout();}bool active=false;
            for(int i=0;i<5;i++)
            {
                Vector2 next=targets[i];if(!ReducedMotion&&targetsReady&&!(dragging&&Selected==i))next=Vector2.Lerp(current[i],next,1-Mathf.Exp(-Time.unscaledDeltaTime*(IsRevealing?5:17)));
                if(Vector2.Distance(current[i],next)>0.05f){current[i]=next;active=true;}else current[i]=targets[i];
                // Transform movement avoids rebuilding rounded image geometry every frame.
                float scale=panel.scale;
                nodes[i].style.translate=new Translate(Mathf.Round((center.x+current[i].x-nodeSizes[i].x*0.5f)*scale)/scale,Mathf.Round((center.y+current[i].y-nodeSizes[i].y*0.5f+PhotoOffset)*scale)/scale,0);
            }
            if(paletteTime<1){paletteTime=Mathf.Min(1,paletteTime+Time.unscaledDeltaTime/1.65f);float t=paletteTime*paletteTime*(3-2*paletteTime);theme=Palette.Lerp(fromPalette,toPalette,t);ApplyTheme();active=true;}
            if(active||field.NeedsUpdate)DrawField();
        }
        void SolveLayout()
        {
            float width=board.resolvedStyle.width,height=board.resolvedStyle.height;if(float.IsNaN(width)||float.IsNaN(height)||width<10||height<10)return;
            center=new Vector2(compact?16:200,60);radius=Mathf.Max(100,width-center.x-(hasRail?340:100));innerRadius=compact?55:80;
            centerGroup.style.left=0;centerGroup.style.top=compact?0:center.y-(images?65:20);centerGroup.style.width=compact?120:150;
            targets=new Vector2[5];
            for(int i=0;i<5;i++)
            {
                float distance=State.positions[i].magnitude;
                float laneY=Array.IndexOf(laneOrder,i)*Mathf.Max(100,(height-120)/5);
                targets[i]=new Vector2(ToPhysical(distance),laneY);
                ghosts[i]=new Vector2(ToPhysical(Rules.ReferenceRadius(Puzzle,i)),laneY);
            }
            if(!targetsReady){current=(Vector2[])targets.Clone();targetsReady=true;}field.NeedsUpdate=true;DrawField();
        }
        float ToPhysical(float normalized)=>Mathf.Lerp(innerRadius,radius,Mathf.Clamp01((normalized-Rules.MinimumRadius)/(Rules.MaximumRadius-Rules.MinimumRadius)));
        Vector2 ToNormalized(Vector2 physical){float value=Mathf.Lerp(Rules.MinimumRadius,Rules.MaximumRadius,Mathf.Clamp01((physical.x-innerRadius)/Mathf.Max(1,radius-innerRadius)));return Vector2.right*value;}
        void DrawField(){field.Configure(center,radius,State,current,ghosts,Selected,theme.Guide,theme.Accent,correcting,innerRadius,awarded);}
        void NodeDown(int index,PointerDownEvent e)
        {
            if(e.button!=0||overlay!=null||IsRevealing)return;Select(index);nodes[index].Focus();e.StopPropagation();if(State.revealed)return;
            dragging=true;moved=false;dragPointer=e.pointerId;pointerStart=board.WorldToLocal(e.position);dragOffset=center+current[index]-pointerStart;nodes[index].CapturePointer(e.pointerId);nodes[index].AddToClassList("dragging");
        }
        void NodeMove(int index,PointerMoveEvent e){if(!dragging||index!=Selected||e.pointerId!=dragPointer)return;var point=board.WorldToLocal(e.position);if(Vector2.Distance(point,pointerStart)>4)moved=true;if(moved)Place(index,point+dragOffset,false);}
        void NodeUp(int index,PointerUpEvent e)
        {
            if(!dragging||index!=Selected||e.pointerId!=dragPointer)return;if(moved)Place(index,board.WorldToLocal(e.position)+dragOffset);else{notice="Choose a place for "+Puzzle.topics[index].label+".";UpdateUI();}
            dragging=false;nodes[index].RemoveFromClassList("dragging");nodes[index].ReleasePointer(e.pointerId);layoutDirty=true;e.StopPropagation();
        }
        void NodeKey(int index,KeyDownEvent e)
        {
            if(overlay!=null||IsRevealing)return;Select(index);if(State.revealed)return;float value=State.positions[index].magnitude,step=e.shiftKey?0.09f:0.035f;
            switch(e.keyCode){case KeyCode.UpArrow:case KeyCode.LeftArrow:value-=step;break;case KeyCode.DownArrow:case KeyCode.RightArrow:value+=step;break;case KeyCode.Return:case KeyCode.Space:break;default:return;}
            PlaceNormalized(index,Vector2.right*value);root.focusController.IgnoreEvent(e);e.StopPropagation();
        }
        void Select(int index){if(Selected==index)return;Selected=index;notice=null;UpdateUI();}
        public void Place(int index,Vector2 localPosition,bool persist=true){if(State.revealed||IsRevealing||radius<=0)return;State.positions[index]=Rules.ClampPosition(ToNormalized(localPosition-center));State.placed[index]=true;notice=null;layoutDirty=true;if(persist)Save();UpdateUI();}
        public void PlaceNormalized(int index,Vector2 value){if(State.revealed||IsRevealing)return;State.positions[index]=Rules.ClampPosition(value);State.placed[index]=true;notice=null;layoutDirty=true;Save();UpdateUI();}
        void Nudge(float amount){if(Selected<0||State.revealed)return;var p=State.positions[Selected];PlaceNormalized(Selected,p.normalized*(p.magnitude+amount));}
        public void Reveal()
        {
            if(State.revealed||IsRevealing)return;
            if(dragging){dragging=false;nodes[Selected].RemoveFromClassList("dragging");nodes[Selected].ReleasePointer(dragPointer);}
            topicPoints=Rules.TopicPoints(Puzzle,State);awardsShown=0;revealedPoints=0;correcting=false;
            revealOrder=(int[])laneOrder.Clone();Array.Clear(awarded,0,awarded.Length);
            State.revealed=true;Selected=-1;Save();IsRevealing=true;UpdateUI();StartCoroutine(FinishReveal());
        }
        IEnumerator FinishReveal()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            for(int step=0;step<revealOrder.Length;step++)
            {
                int i=revealOrder[step];Selected=i;awarded[i]=true;awardsShown=step+1;revealedPoints+=topicPoints[i];UpdateUI();
                yield return new WaitForSecondsRealtime(0.75f);
            }
            Selected=-1;correcting=true;layoutDirty=true;IsRevealing=false;UpdateUI();
        }
        void UpdateUI()
        {
            if(State==null)return;
            if(browserUI){PublishBrowserState();return;}
            action.text=IsRevealing?(correcting?"Correcting…":"Counting points…"):State.revealed?"Next":"Reveal";action.SetEnabled(!IsRevealing);share.style.display=State.revealed&&!IsRevealing?DisplayStyle.Flex:DisplayStyle.None;
            instruction.style.display=State.revealed?DisplayStyle.None:DisplayStyle.Flex;score.style.display=State.revealed?DisplayStyle.Flex:DisplayStyle.None;
            if(State.revealed){var s=Rules.Score(Puzzle,State);score.text=(IsRevealing?revealedPoints:s.percent)+" / 100 points"+(IsRevealing?"":"     "+s.correct+" of "+s.total+" comparisons");}
            status.text=notice??(State.revealed?(IsRevealing?(correcting?"Now showing the reference distances.":"Each correct comparison splits its points between two topics."):"Select a topic to see why it connects."):"Reveal whenever you're ready.");adjust.style.display=Selected>=0&&!State.revealed?DisplayStyle.Flex:DisplayStyle.None;imagesButton.text=images?"Images on":"Images off";
            for(int i=0;i<5;i++){nodes[i].EnableInClassList("active",Selected==i);nodes[i].EnableInClassList("unplaced",!State.placed[i]);nodes[i].tooltip=Puzzle.topics[i].label;pointBadges[i].text="+"+topicPoints[i]+" points";pointBadges[i].style.display=State.revealed&&awarded[i]?DisplayStyle.Flex:DisplayStyle.None;}UpdateEvidence();field.NeedsUpdate=true;
        }
        void UpdateEvidence()
        {
            Vector2 scrollOffset=evidence?.Q<ScrollView>()?.scrollOffset??Vector2.zero;
            evidence?.RemoveFromHierarchy();evidence=null;if(State==null||!State.revealed||IsRevealing)return;
            evidence=Element(app,hasRail?"connection-panel":"evidence");
            Text(evidence,"Shared links","connection-title");
            Text(evidence,"Outgoing links in common; similarity adjusts for article size.","evidence-caption");
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("connection-scroll");evidence.Add(scroll);
            foreach(int index in revealOrder)AddPathRow(scroll,index,false);
            scroll.schedule.Execute(()=>scroll.scrollOffset=scrollOffset);
            Button(evidence,"Sources and image credits",ShowAllConnections,"quiet all-connections");
        }
        void AddPathRow(VisualElement parent,int index,bool detailed)
        {
            var topic=Puzzle.topics[index];var path=connectionPaths[index];var row=Element(parent,"connection-row");row.name="connection-"+index;row.EnableInClassList("selected-path",Selected==index);
            Button(row,topic.label+"   +"+topicPoints[index],()=>Select(index),"quiet connection-name");
            Text(row,topic.sharedCount+" shared links","path-note");
            var examples=topic.evidence??new string[0];
            Text(row,string.Join(" \u00b7 ",examples,0,Math.Min(detailed?8:3,examples.Length)),"path-note");
            if(detailed&&path.Length>0)Text(row,string.Join(" \u2192 ",path),"path-note");
            if(detailed)Button(row,"Read "+topic.label,()=>OpenLink(topic.url),"quiet source-link");
        }
        void Share()
        {
            var result=Rules.Score(Puzzle,State);string text="Neardle\n"+Puzzle.anchor+"\n"+result.percent+" points\n"+result.correct+" of "+result.total+" connections";
#if UNITY_WEBGL && !UNITY_EDITOR
            NearerCopy(text);
#else
            GUIUtility.systemCopyBuffer=text;OnClipboard("copied");
#endif
        }
        public void OnClipboard(string outcome){notice=outcome=="copied"?"Result copied.":"Copy is unavailable in this browser.";UpdateUI();}
        public void SetReducedMotion(bool reduced){ReducedMotion=reduced;Write("nearer.motion",reduced?"reduced":"full");if(browserUI){UpdateUI();return;}if(reduced){paletteTime=1;theme=toPalette;ApplyTheme();}}
        void ShowHelp()
        {
            var sheet=NewSheet("A sense of connection");Text(sheet,"Move stronger connections left, nearer the reference line. Weaker connections belong farther right.","sheet-copy");Text(sheet,"Each topic stays in its own lane. Drag horizontally or use the arrow keys. Reveal shows points and target circles from top to bottom.","sheet-copy");Text(sheet,"0-100 shows relative distance based on shared-link similarity. The result panel shows shared-link counts and examples.","sheet-copy");
            var motion=Button(sheet,ReducedMotion?"Motion reduced":"Motion on",null,"quiet sheet-option");motion.clicked+=()=>{SetReducedMotion(!ReducedMotion);motion.text=ReducedMotion?"Motion reduced":"Motion on";};Button(sheet,"Back",CloseOverlay,"action sheet-close");
        }
        void ShowSources()
        {
            if(State.revealed&&!IsRevealing){ShowAllConnections();return;}
            var sheet=NewSheet("The connections");Text(sheet,"We compare the article links shared by two Wikipedia pages, adjusted for their total links. Exact pixel positions do not count. Near ties accept either order. Each correct comparison splits its points between the two topics; whole-number awards add up to your score.","sheet-copy");Text(sheet,"Questions are generated from a growing source collection. The first map is shared each day. Text is credited to Wikipedia contributors under CC BY-SA 4.0.","sheet-copy");Button(sheet,"Read "+Puzzle.anchor,()=>OpenLink(Puzzle.anchorUrl),"quiet source-link");Button(sheet,"Image credits",ShowImageCredits,"quiet source-link");Button(sheet,"Text license",()=>OpenLink(Bank.licenseUrl),"quiet source-link");Button(sheet,"Back",CloseOverlay,"action sheet-close");
        }
        void ShowAllConnections()
        {
            var sheet=NewSheet("All five connections");sheet.AddToClassList("connections-sheet");
            Text(sheet,"Each arrow follows a Wikipedia article link. These are the shortest directed paths in the saved article collection; Wikipedia may have shorter routes outside it. Scoring still compares shared links, not path length.","sheet-copy");
            var scroll=new ScrollView();scroll.AddToClassList("sheet-scroll");sheet.Add(scroll);
            foreach(int index in revealOrder)AddPathRow(scroll,index,true);
            Button(sheet,"Read "+Puzzle.anchor,()=>OpenLink(Puzzle.anchorUrl),"quiet source-link");
            Button(sheet,"Image credits",ShowImageCredits,"quiet source-link");Button(sheet,"Text license",()=>OpenLink(Bank.licenseUrl),"quiet source-link");
            Button(sheet,"Back",CloseOverlay,"action sheet-close");
        }
        void ShowImageCredits()
        {
            var sheet=NewSheet("Image credits");var scroll=new ScrollView();scroll.AddToClassList("sheet-scroll");sheet.Add(scroll);var titles=new HashSet<string>{Puzzle.anchorTitle};foreach(var t in Puzzle.topics)titles.Add(t.title);
            foreach(var credit in media.entries)if(titles.Contains(credit.title)){var row=Element(scroll,"credit");Text(row,credit.title,"credit-title");Text(row,credit.author+". "+credit.license,"sheet-copy");string url=credit.url;Button(row,"Source image",()=>OpenLink(url),"quiet source-link");}Button(sheet,"Back",CloseOverlay,"action sheet-close");
        }
        VisualElement NewSheet(string title)
        {
            CloseOverlay();overlay=Element(app,"overlay");overlay.style.backgroundColor=theme.Base;var sheet=Element(overlay,"sheet");Text(sheet,title,"sheet-title");
            overlay.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode!=KeyCode.Tab)return;var buttons=overlay.Query<Button>().ToList();if(buttons.Count==0)return;int currentIndex=buttons.FindIndex(b=>b==root.focusController.focusedElement);buttons[(currentIndex+(e.shiftKey?-1:1)+buttons.Count)%buttons.Count].Focus();root.focusController.IgnoreEvent(e);e.StopImmediatePropagation();});overlay.schedule.Execute(()=>overlay?.Query<Button>().First()?.Focus());return sheet;
        }
        void CloseOverlay(){overlay?.RemoveFromHierarchy();overlay=null;}
        static void OpenLink(string url){if(url.StartsWith("https://en.wikipedia.org/",StringComparison.Ordinal)||url.StartsWith("https://commons.wikimedia.org/",StringComparison.Ordinal)||url.StartsWith("https://creativecommons.org/",StringComparison.Ordinal))Application.OpenURL(url);}
        void ApplyTheme()
        {
            root.style.color=theme.Ink;panel.colorClearValue=theme.Base;
            for(int y=0;y<32;y++)for(int x=0;x<32;x++){float u=x/31f,v=y/31f;float t=Mathf.Clamp01(u*0.55f+v*0.42f);washPixels[y*32+x]=Color.Lerp(theme.Base,theme.Second,t);}
            wash.SetPixels(washPixels);wash.Apply(false);action.style.backgroundColor=theme.Accent;
            for(int i=0;i<5;i++){photos[i].style.borderTopColor=theme.Accent;photos[i].style.borderBottomColor=theme.Accent;photos[i].style.borderLeftColor=theme.Accent;photos[i].style.borderRightColor=theme.Accent;if(photos[i].ClassListContains("no-image"))photos[i].style.backgroundColor=Color.Lerp(theme.Base,theme.Accent,0.2f);}field.NeedsUpdate=true;
        }
        struct Palette
        {
            public Color Base,Second,Accent,Ink,Guide;
            public static Palette At(int i)
            {
                string[,] colors={{"#E5EEF1","#F2EFE8","#386378"},{"#ECE8F1","#E7EFF0","#69558A"},{"#F2E8E5","#F0E9F1","#8B5263"},{"#E8EEE3","#E4EEF0","#476C57"},{"#E5EBF4","#F0ECE9","#49658B"}};i=((i%5)+5)%5;ColorUtility.TryParseHtmlString(colors[i,0],out Color a);ColorUtility.TryParseHtmlString(colors[i,1],out Color b);ColorUtility.TryParseHtmlString(colors[i,2],out Color c);return new Palette{Base=a,Second=b,Accent=c,Ink=new Color32(37,47,56,255),Guide=Color.Lerp(a,c,0.18f)};
            }
            public static Palette Lerp(Palette a,Palette b,float t)=>new Palette{Base=Color.Lerp(a.Base,b.Base,t),Second=Color.Lerp(a.Second,b.Second,t),Accent=Color.Lerp(a.Accent,b.Accent,t),Ink=Color.Lerp(a.Ink,b.Ink,t),Guide=Color.Lerp(a.Guide,b.Guide,t)};
        }
        string Read(string key){try{
#if UNITY_WEBGL && !UNITY_EDITOR
            return NearerRead(key);
#else
            return PlayerPrefs.GetString(key,"");
#endif
        }catch{return "";}}
        void Write(string key,string value){try{
#if UNITY_WEBGL && !UNITY_EDITOR
            if(NearerWrite(key,value)==0)notice="Progress stays here while this tab is open.";
#else
            PlayerPrefs.SetString(key,value);PlayerPrefs.Save();
#endif
        }catch{notice="Progress stays here while this tab is open.";}}
        void Save(){Write(StorageKey,JsonUtility.ToJson(State));}
        public void BrowserInput(string json)
        {
            if(!browserUI)return;
            BrowserCommand command;try{command=JsonUtility.FromJson<BrowserCommand>(json);}catch{return;}
            if(command==null)return;
            switch(command.action)
            {
                case "sync":UpdateUI();break;
                case "move":if(command.index>=0&&command.index<5&&!float.IsNaN(command.distance)&&!float.IsInfinity(command.distance))PlaceNormalized(command.index,Vector2.right*Mathf.Lerp(Rules.MinimumRadius,Rules.MaximumRadius,Mathf.Clamp01(command.distance)));break;
                case "select":if(command.index>=0&&command.index<5){Selected=command.index;UpdateUI();}break;
                case "reveal":Reveal();break;
                case "next":if(State.revealed)Next();break;
                case "images":ToggleImages();break;
                case "motion":SetReducedMotion(command.value=="reduced");break;
                case "share":if(State.revealed&&!IsRevealing)Share();break;
            }
        }
        void PublishBrowserState()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var result=Rules.Score(Puzzle,State);var topics=new BrowserTopic[5];
            for(int i=0;i<5;i++)
            {
                var path=connectionPaths[i];var urls=new string[path.Length];for(int step=0;step<path.Length;step++)urls[step]=stream.ArticleUrl(path[step]);
                topics[i]=new BrowserTopic{title=Puzzle.topics[i].title,label=Puzzle.topics[i].label,url=Puzzle.topics[i].url,distance=Mathf.InverseLerp(Rules.MinimumRadius,Rules.MaximumRadius,State.positions[i].magnitude),reference=Mathf.InverseLerp(Rules.MinimumRadius,Rules.MaximumRadius,Rules.ReferenceRadius(Puzzle,i)),points=topicPoints[i],awarded=awarded[i],sharedCount=Puzzle.topics[i].sharedCount,evidence=Puzzle.topics[i].evidence,path=path,pathUrls=urls};
            }
            NearerPublishState(JsonUtility.ToJson(new BrowserState{puzzleId=Puzzle.id,anchor=Puzzle.anchor,anchorTitle=Puzzle.anchorTitle,anchorUrl=Puzzle.anchorUrl,question=question,selected=Selected,score=IsRevealing?revealedPoints:result.percent,correct=result.correct,total=result.total,revealed=State.revealed,animating=IsRevealing,correcting=correcting,images=images,reducedMotion=ReducedMotion,topics=topics,order=State.revealed?revealOrder:laneOrder,notice=notice}));
#endif
        }
        [Serializable]class BrowserCommand{public string action,value;public int index;public float distance;}
        [Serializable]class BrowserState{public string puzzleId,anchor,anchorTitle,anchorUrl,notice;public int question,selected,score,correct,total;public int[] order;public bool revealed,animating,correcting,images,reducedMotion;public BrowserTopic[] topics;}
        [Serializable]class BrowserTopic{public string title,label,url;public float distance,reference;public int points,sharedCount;public bool awarded;public string[] path,pathUrls,evidence;}
        public string Snapshot()
        {
            var s=Rules.Score(Puzzle,State);int overlaps=0;for(int i=0;i<5;i++)for(int j=i+1;j<5;j++)if(MagnetLayout.Overlap(new Rect(targets[i]-nodeSizes[i]*0.5f,nodeSizes[i]),new Rect(targets[j]-nodeSizes[j]*0.5f,nodeSizes[j]))>1)overlaps++;
            return JsonUtility.ToJson(new RuntimeSnapshot{puzzle=Puzzle.id,anchor=Puzzle.anchor,question=question,revealed=State.revealed,animating=IsRevealing,reducedMotion=ReducedMotion,score=s.percent,correct=s.correct,total=s.total,overlaps=overlaps,width=root.resolvedStyle.width,height=root.resolvedStyle.height,state=State,awardsShown=awardsShown,displayedPoints=revealedPoints,correcting=correcting,selected=Selected,revealOrder=revealOrder,awarded=awarded,displayPositions=current,textureSlots=(int)panel.textureSlotCount,sidePathCount=evidence==null?0:evidence.Query<VisualElement>(className:"connection-row").ToList().Count});
        }
        [Serializable]class RuntimeSnapshot{public string puzzle,anchor;public int question,score,correct,total,overlaps,awardsShown,displayedPoints,selected,textureSlots,sidePathCount;public int[] revealOrder;public bool[] awarded;public Vector2[] displayPositions;public float width,height;public bool revealed,animating,reducedMotion,correcting;public RoundState state;}
        void OnDestroy(){if(wash!=null)Destroy(wash);if(Instance==this)Instance=null;}
    }
}
