using UnityEngine;
using UnityEngine.UIElements;
namespace Nearer
{
    public sealed class FieldDrawing:VisualElement
    {
        public static readonly Color Mineral=new Color32(229,238,241,255);
        public bool NeedsUpdate=true;
        readonly VisualElement reference;
        readonly VisualElement[] guides=new VisualElement[5],originals=new VisualElement[5],corrections=new VisualElement[5];
        readonly Label near,far;
        readonly VisualElement[] ticks=new VisualElement[255];
        readonly Label[] numbers=new Label[55];
        readonly VisualElement[,] dots=new VisualElement[5,25];
        public FieldDrawing()
        {
            pickingMode=PickingMode.Ignore;reference=Line();near=Caption("STRONGER / NEARER");far=Caption("WEAKER / FARTHER");
            for(int tick=0;tick<255;tick++)ticks[tick]=Line();
            for(int n=0;n<55;n++)numbers[n]=Caption(((n%11)*10).ToString());
            for(int i=0;i<5;i++)
            {
                guides[i]=Line();originals[i]=Line();corrections[i]=Line();
                originals[i].style.borderTopWidth=originals[i].style.borderRightWidth=originals[i].style.borderBottomWidth=originals[i].style.borderLeftWidth=2;
                originals[i].style.borderTopLeftRadius=originals[i].style.borderTopRightRadius=originals[i].style.borderBottomLeftRadius=originals[i].style.borderBottomRightRadius=30;
                for(int dot=0;dot<25;dot++)dots[i,dot]=Line();
            }
        }
        VisualElement Line(){var line=new VisualElement{pickingMode=PickingMode.Ignore};line.style.position=Position.Absolute;Add(line);return line;}
        Label Caption(string text){var label=new Label(text){pickingMode=PickingMode.Ignore};label.style.position=Position.Absolute;label.style.fontSize=10;label.style.color=(Color)new Color32(99,116,128,255);Add(label);return label;}
        public void Configure(Vector2 origin,float size,RoundState round,Vector2[] display,Vector2[] original,int selection,Color guide,Color accent,bool correcting,float minimum=0,bool[] awarded=null)
        {
            reference.style.display=near.style.display=far.style.display=DisplayStyle.None;
            foreach(var tick in ticks)tick.style.display=DisplayStyle.None;foreach(var number in numbers)number.style.display=DisplayStyle.None;
            Color green=new Color(0.17f,0.49f,0.30f,1);
            for(int i=0;i<5;i++)
            {
                var ring=guides[i];ring.style.display=i<4?DisplayStyle.Flex:DisplayStyle.None;
                float r=Mathf.Lerp(minimum,size,i/3f);Set(ring,origin.x-r,origin.y-r,r*2,r*2,Color.clear);
                ring.style.borderTopWidth=ring.style.borderRightWidth=ring.style.borderBottomWidth=ring.style.borderLeftWidth=1;
                ring.style.borderTopLeftRadius=ring.style.borderTopRightRadius=ring.style.borderBottomLeftRadius=ring.style.borderBottomRightRadius=r;
                var tint=new Color(accent.r,accent.g,accent.b,.18f);ring.style.borderTopColor=ring.style.borderRightColor=ring.style.borderBottomColor=ring.style.borderLeftColor=tint;
                corrections[i].style.display=DisplayStyle.None;
                bool show=round.revealed&&(awarded==null?correcting:awarded[i]);originals[i].style.display=show?DisplayStyle.Flex:DisplayStyle.None;
                originals[i].style.borderTopColor=originals[i].style.borderRightColor=originals[i].style.borderBottomColor=originals[i].style.borderLeftColor=green;
                Set(originals[i],origin.x+original[i].x-30,origin.y+original[i].y-30,60,60,Color.clear);
                originals[i].style.borderTopLeftRadius=originals[i].style.borderTopRightRadius=originals[i].style.borderBottomLeftRadius=originals[i].style.borderBottomRightRadius=30;
                originals[i].style.opacity=selection<0||selection==i?1:.55f;
                for(int dot=0;dot<25;dot++)
                {
                    var mark=dots[i,dot];mark.style.display=show&&Vector2.Distance(original[i],display[i])>1?DisplayStyle.Flex:DisplayStyle.None;
                    Vector2 point=origin+Vector2.Lerp(display[i],original[i],dot/24f);Set(mark,point.x,point.y,3,3,green);
                }
            }
            NeedsUpdate=false;
        }
        static void Set(VisualElement element,float x,float y,float width,float height,Color color){element.style.left=x;element.style.top=y;element.style.width=width;element.style.height=height;element.style.backgroundColor=color;}
    }
}
