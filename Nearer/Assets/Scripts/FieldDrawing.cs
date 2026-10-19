using UnityEngine;
using UnityEngine.UIElements;
namespace Nearer
{
    public sealed class FieldDrawing:VisualElement
    {
        public static readonly Color Mineral=new Color32(229,238,241,255);
        public bool NeedsUpdate=true;
        Vector2 center;float radius;RoundState state;Vector2[] positions,ghosts;int selected;Color guide,accent;
        public FieldDrawing(){pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;}
        public void Configure(Vector2 origin,float size,RoundState round,Vector2[] display,Vector2[] original,int selection,Color guideColor,Color accentColor){center=origin;radius=size;state=round;positions=display;ghosts=original;selected=selection;guide=guideColor;accent=accentColor;NeedsUpdate=false;MarkDirtyRepaint();}
        void Draw(MeshGenerationContext context)
        {
            if(state==null||radius<=0)return;var p=context.painter2D;p.lineWidth=1;
            for(int i=0;i<positions.Length;i++)
            {
                float orbit=positions[i].magnitude;bool sharedOrbit=false;
                for(int j=0;j<i;j++)if(Mathf.Abs(positions[j].magnitude-orbit)<0.75f){sharedOrbit=true;break;}
                if(!sharedOrbit){p.strokeColor=new Color(accent.r,accent.g,accent.b,0.105f);Circle(p,center,orbit);}
            }
            if(selected>=0){p.lineWidth=1.3f;p.strokeColor=new Color(accent.r,accent.g,accent.b,0.38f);Circle(p,center,positions[selected].magnitude);p.lineWidth=1;}
            if(state.revealed)for(int i=0;i<5;i++){var before=center+ghosts[i];var after=center+positions[i];p.strokeColor=new Color(accent.r,accent.g,accent.b,0.3f);Circle(p,before,3);var delta=after-before;float length=delta.magnitude;if(length<12)continue;var d=delta.normalized;p.strokeColor=new Color(accent.r,accent.g,accent.b,selected==i?0.35f:0.15f);p.BeginPath();p.MoveTo(before+d*7);p.LineTo(after-d*12);p.Stroke();}
        }
        static void Circle(Painter2D p,Vector2 point,float size){p.BeginPath();p.Arc(point,size,0,360);p.Stroke();}
    }
}
