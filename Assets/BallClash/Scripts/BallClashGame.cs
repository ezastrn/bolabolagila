using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallClash
{
    /// <summary>
    /// Unity/C# port of Ball Clash. Rendering deliberately remains procedural so the original canvas look
    /// (glows, grid, balls, trails and combat fields) has no external sprite dependency.
    /// </summary>
    public sealed class BallClashGame : MonoBehaviour
    {
        const float ArenaW = 1000f, ArenaH = 620f, Radius = 31f, BaseSpeed = 4.1f, MaxSpeed = BaseSpeed * 3f;
        readonly Dictionary<string, BallDefinition> balls = new Dictionary<string, BallDefinition>();
        readonly List<Particle> particles = new List<Particle>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Field> fields = new List<Field>();
        readonly List<FloatingText> texts = new List<FloatingText>();
        BallState player, enemy;
        string chosen = "fire", enemyType = "ice";
        Role selectedRole = Role.Fighter;
        int difficulty;
        Phase phase = Phase.Menu;
        float elapsed, botDecision, botSkillClock;
        bool aiming;
        Vector2 aim;
        Texture2D pixel, disc;
        GUIStyle title, heading, normal, small, center, button, selectedButton, hud;

        enum Phase { Menu, Aim, Fight, End }
        sealed class BallState
        {
            public Vector2 Pos, Velocity; public float Hp = 100, Energy = 100, Slow, Stun, Poison, Invincible, Boost;
            public bool Armed; public int WallHits; public float RangedCooldown = 1f;
        }
        struct Particle { public Vector2 P, V; public Color C; public float Life, Size; }
        struct Shot { public Vector2 P, V; public bool FromPlayer, Magic; public float Life, Damage; public Color C; }
        struct Field { public Ability Ability; public bool FromPlayer; public Vector2 P; public float Life, Radius, Tick; }
        struct FloatingText { public Vector2 P; public string Value; public Color C; public float Life; }

        void Awake()
        {
            Application.targetFrameRate = 60;
            pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply();
            disc = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32));
                disc.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((32f - d) * 2f)));
            }
            disc.Apply();
            Add("fire", Role.Fighter, "🔥", "FIRE", "METEOR CRASH", "#ef4444", "Boost kecepatan untuk benturan maksimum.", 1.25f, 1f, .94f, Ability.Blast);
            Add("ice", Role.Magic, "❄️", "ICE", "FREEZE", "#38bdf8", "Tiga serangan area yang memperlambat.", .92f, .97f, 1.14f, Ability.Freeze);
            Add("thunder", Role.Fighter, "⚡", "THUNDER", "LIGHTNING DASH", "#facc15", "Dash ekstrem menuju lawan.", 1.08f, 1.18f, .9f, Ability.Dash);
            Add("spider", Role.Marksman, "🕷️", "SPIDER", "WEB WALL", "#ef3340", "Jaring dinding yang memperlambat dan melukai.", 1.04f, 1.08f, 1.02f, Ability.Web);
            Add("shadow", Role.Fighter, "🌑", "SHADOW", "VOID STEP", "#a78bfa", "Teleport lalu memberi racun.", 1.1f, 1.06f, .93f, Ability.Shadow);
            Add("wind", Role.Marksman, "🌪️", "WIND", "TORNADO", "#34d399", "Tornado pemburu yang memberi stun.", .9f, 1.25f, .92f, Ability.Knock);
            Add("crystal", Role.Magic, "💎", "CRYSTAL", "PRISM SHIELD", "#f472b6", "Kebal singkat dan proyektil besar.", .8f, .88f, 1.42f, Ability.Shield);
            Add("blackhole", Role.Magic, "🕳️", "BLACK HOLE", "GRAVITY WELL", "#8b5cf6", "Menarik musuh menuju pengguna.", 1.02f, .92f, 1.08f, Ability.BlackHole);
            MakeStyles();
        }
        void Add(string id, Role role, string icon, string name, string skill, string hex, string desc, float dmg, float speed, float def, Ability ability)
            => balls.Add(id, new BallDefinition(id, role, icon, name, skill, ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white, desc, dmg, speed, def, ability));
        void MakeStyles()
        {
            title = Style(46, FontStyle.Bold, Color.white); heading = Style(19, FontStyle.Bold, Color.white);
            normal = Style(14, FontStyle.Normal, new Color(.83f,.87f,.96f)); small = Style(12, FontStyle.Normal, new Color(.60f,.67f,.79f));
            center = Style(14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter); hud = Style(13, FontStyle.Bold, Color.white);
            button = Style(14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft); button.normal.background = Tex("#0c1220"); button.hover.background = Tex("#151d30");
            selectedButton = Style(14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft); selectedButton.normal.background = Tex("#242044"); selectedButton.hover.background = Tex("#30285a");
        }
        GUIStyle Style(int size, FontStyle weight, Color c, TextAnchor align = TextAnchor.UpperLeft) { return new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight, normal = { textColor = c }, alignment = align, wordWrap = true, padding = new RectOffset(10,10,7,7) }; }
        Texture2D Tex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); var t = new Texture2D(1,1); t.SetPixel(0,0,c); t.Apply(); return t; }
        void OnGUI()
        {
            var scale = Mathf.Min(Screen.width / 1200f, Screen.height / 820f); var old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width-1200*scale)/2,(Screen.height-820*scale)/2), Quaternion.identity, Vector3.one*scale);
            DrawBackground();
            if (phase == Phase.Menu) DrawMenu(); else DrawGame();
            GUI.matrix = old;
        }
        void DrawBackground() { GUI.DrawTexture(new Rect(0,0,1200,820), Tex("#070a12")); GUI.Label(new Rect(45,22,760,30), "BALL CLASH / RICOCHET DUEL", hud); GUI.Label(new Rect(45,48,870,24), "Aim once. Release. Fighter = close range · Magic = ranged energy · Marksman = ranged speed.", small); }
        void DrawMenu()
        {
            Panel(new Rect(35,85,1130,700), "#0b101c"); GUI.Label(new Rect(70,120,550,105), "DUEL\nBY BOUNCE.", title);
            GUI.Label(new Rect(70,230,560,56), "Pilih bola, tentukan sudut dan power sekali di awal ronde. Setelah dilepas, kontrol berhenti. Bola akan memantul sendiri dan mencoba menemukan lawannya.", normal);
            GUI.Label(new Rect(70,305,420,28), "Pilih Kelas & Kekuatan", heading);
            var roles = new[] { Role.Fighter, Role.Magic, Role.Marksman }; var labels = new[] { "🥊 FIGHTER\nJarak dekat · tahan banting", "✨ MAGIC\nProyektil energi · area control", "🎯 MARKSMAN\nSerangan jauh · cepat" };
            for (int i = 0; i < 3; i++)
            {
                if (!GUI.Button(new Rect(70 + i * 178, 342, 165, 62), labels[i], selectedRole == roles[i] ? selectedButton : button)) continue;
                selectedRole = roles[i];
                if (balls[chosen].Role == selectedRole) continue;
                foreach (var candidate in balls.Values)
                    if (candidate.Role == selectedRole) { chosen = candidate.Id; break; }
            }
            int col = 0, row = 0;
            foreach (var candidate in balls.Values)
            {
                if (candidate.Role != selectedRole) continue;
                var pickRect = new Rect(70 + col * 178, 420 + row * 78, 165, 68);
                if (GUI.Button(pickRect, candidate.Icon + "  " + candidate.DisplayName + "\n" + candidate.Skill, chosen == candidate.Id ? selectedButton : button)) chosen = candidate.Id;
                col++;
                if (col == 3) { col = 0; row++; }
            }
            GUI.Label(new Rect(70,595,300,28), "Kesulitan Bot", heading);
            string[] levels={"🟢 EASY\nBot asal memakai skill.","🟡 MEDIUM\nBot membaca situasi.","🔴 HARD\nBot mencari timing terbaik."};
            for(int i=0;i<3;i++) if(GUI.Button(new Rect(70+i*178,632,165,55),levels[i],difficulty==i?selectedButton:button))difficulty=i;
            if(GUI.Button(new Rect(70,710,210,48),"START DUEL",selectedButton)) StartRound();
            var b=balls[chosen]; Panel(new Rect(685,150,410,470),"#0a0f1c"); GUI.Label(new Rect(710,190,360,35),"PREVIEW",center); DrawBall(new Vector2(890,390),60,b,null); GUI.Label(new Rect(720,505,340,35),b.Icon+"  "+b.DisplayName,Style(25,FontStyle.Bold,b.Color,TextAnchor.MiddleCenter)); GUI.Label(new Rect(720,545,340,60),b.Description,small);
        }
        void StartRound()
        {
            var candidates=new List<string>(balls.Keys); candidates.Remove(chosen); enemyType=candidates[UnityEngine.Random.Range(0,candidates.Count)];
            player=new BallState{Pos=new Vector2(170,ArenaH/2)}; enemy=new BallState{Pos=new Vector2(ArenaW-170,ArenaH/2),RangedCooldown=1.65f};
            particles.Clear();shots.Clear();fields.Clear();texts.Clear();elapsed=0;phase=Phase.Aim; botDecision=.5f; botSkillClock=0;
        }
        void DrawGame()
        {
            DrawHud(); var arena=new Rect(100,150,ArenaW,ArenaH); DrawArena(arena); HandleInput(arena);
            GUI.Label(new Rect(100,785,620,25), phase==Phase.Aim?"DRAG DARI BOLA PLAYER UNTUK MENENTUKAN ARAH & POWER":"Bola bergerak otomatis. Pantulan pertama netral; pantulan berikutnya mendapat boost. Speed cap 3×.",small);
            var skill = balls[chosen].Skill; if(GUI.Button(new Rect(850,778,250,36), skill, player.Energy>=100&&phase==Phase.Fight?selectedButton:button) && player.Energy>=100&&phase==Phase.Fight) UseSkill(true);
            if(phase==Phase.End) { Panel(new Rect(385,345,430,200),"#0d1423"); bool win=enemy.Hp<=0; GUI.Label(new Rect(405,370,390,54),win?"VICTORY 🏆":"DEFEAT 💥",Style(34,FontStyle.Bold,Color.white,TextAnchor.MiddleCenter)); GUI.Label(new Rect(405,430,390,35),win?balls[chosen].DisplayName+" menang.":balls[enemyType].DisplayName+" menemukan timing yang tepat.",center); if(GUI.Button(new Rect(500,480,200,40),"PLAY AGAIN",selectedButton))StartRound(); }
        }
        void DrawHud(){ var pn=balls[chosen]; var en=balls[enemyType]; Panel(new Rect(100,85,380,55),"#0c1220");Panel(new Rect(720,85,380,55),"#0c1220"); GUI.Label(new Rect(110,89,350,18),pn.Icon+" "+pn.DisplayName,hud);GUI.Label(new Rect(730,89,350,18),en.Icon+" "+en.DisplayName,Style(13,FontStyle.Bold,Color.white,TextAnchor.UpperRight)); Bar(new Rect(110,112,350,8),player.Hp,Color.red);Bar(new Rect(110,126,350,6),player.Energy,Color.cyan);Bar(new Rect(730,112,350,8),enemy.Hp,Color.red);Bar(new Rect(730,126,350,6),enemy.Energy,Color.cyan); GUI.Label(new Rect(500,88,200,30),Mathf.FloorToInt(elapsed/60)+":"+Mathf.FloorToInt(elapsed%60).ToString("00"),Style(25,FontStyle.Bold,Color.white,TextAnchor.MiddleCenter)); GUI.Label(new Rect(500,118,200,18),phase==Phase.Aim?"AIM PHASE":"RICHOCHET · "+new[]{"EASY","MEDIUM","HARD"}[difficulty],Style(11,FontStyle.Normal,new Color(.61f,.66f,.77f),TextAnchor.MiddleCenter)); }
        void DrawArena(Rect r){ GUI.DrawTexture(r,Tex("#080d18")); for(int x=40;x<1000;x+=80) Line(r.position+new Vector2(x,0),r.position+new Vector2(x,620),new Color(.08f,.14f,.24f));for(int y=40;y<620;y+=80)Line(r.position+new Vector2(0,y),r.position+new Vector2(1000,y),new Color(.08f,.14f,.24f)); Line(r.position+new Vector2(500,25),r.position+new Vector2(500,595),new Color(.15f,.21f,.33f),2); DrawFields(r); DrawBall(r.position+player.Pos,Radius,balls[chosen],player);DrawBall(r.position+enemy.Pos,Radius,balls[enemyType],enemy); foreach(var s in shots)Circle(r.position+s.P,7,s.C); foreach(var t in texts)GUI.Label(new Rect(r.x+t.P.x-30,r.y+t.P.y-55,60,22),t.Value,Style(14,FontStyle.Bold,t.C,TextAnchor.MiddleCenter)); if(aiming){var end=r.position+player.Pos+(player.Pos-aim).normalized*Mathf.Min(260,Vector2.Distance(player.Pos,aim));Line(r.position+player.Pos,end,Color.white,4);Circle(end,7,Color.white);} }
        void HandleInput(Rect arena)
        { var m=(Event.current.mousePosition-arena.position)*ArenaW/arena.width; if(phase==Phase.Aim){if(Event.current.type==EventType.MouseDown&&Vector2.Distance(m,player.Pos)<70){aiming=true;Event.current.Use();} if(aiming&&Event.current.type==EventType.MouseDrag){aim=m;Event.current.Use();}if(aiming&&Event.current.type==EventType.MouseUp){aiming=false;var d=player.Pos-aim;if(d.magnitude>35){float power=.72f+Mathf.Min(d.magnitude,260)/260*1.18f;player.Velocity=d.normalized*BaseSpeed*balls[chosen].Speed*power;var a=UnityEngine.Random.Range(-.275f,.275f);enemy.Velocity=new Vector2(-BaseSpeed*Mathf.Cos(a),UnityEngine.Random.Range(-.9f,.9f))*balls[enemyType].Speed;phase=Phase.Fight;}Event.current.Use();}} }
        void Update(){if(phase!=Phase.Fight)return;float dt=Mathf.Min(.033f,Time.deltaTime);elapsed+=dt;UpdateBall(player,balls[chosen],dt);UpdateBall(enemy,balls[enemyType],dt);UpdateFields(dt);UpdateShots(dt);Collide();Bot(dt);if(player.Hp<=0||enemy.Hp<=0)phase=Phase.End;}
        void UpdateBall(BallState o,BallDefinition b,float dt){o.Slow=Mathf.Max(0,o.Slow-dt);o.Stun=Mathf.Max(0,o.Stun-dt);o.Poison=Mathf.Max(0,o.Poison-dt);o.Invincible=Mathf.Max(0,o.Invincible-dt);o.Boost=Mathf.Max(0,o.Boost-dt);if(o.Poison>0)Damage(null,o,2.4f,"POISON",new Color(.65f,.5f,1));if(o.Stun<=0)o.Pos+=o.Velocity*60*dt*(o.Slow>0?.45f:1)*(o.Boost>0?1.28f:1);o.Velocity*=Mathf.Pow(.999f,dt*60);if(o.Pos.x<Radius||o.Pos.x>ArenaW-Radius){o.Pos.x=Mathf.Clamp(o.Pos.x,Radius,ArenaW-Radius);o.Velocity.x*=-1;Wall(o,b);}if(o.Pos.y<Radius||o.Pos.y>ArenaH-Radius){o.Pos.y=Mathf.Clamp(o.Pos.y,Radius,ArenaH-Radius);o.Velocity.y*=-1;Wall(o,b);}o.Velocity=Vector2.ClampMagnitude(o.Velocity,MaxSpeed);o.Energy=Mathf.Min(100,o.Energy+dt*5.5f);o.RangedCooldown-=dt;if(b.Role!=Role.Fighter&&o.RangedCooldown<=0)FireShot(o,o==player,b);}
        void Wall(BallState o,BallDefinition b){o.WallHits++;if(o.WallHits>1)o.Velocity=Vector2.ClampMagnitude(o.Velocity*1.14f,MaxSpeed);Burst(o.Pos,b.Color,10);}
        void Collide(){var d=enemy.Pos-player.Pos;if(d.magnitude>=Radius*2)return;var n=d.normalized;player.Pos-=n*(Radius*2-d.magnitude)/2;enemy.Pos+=n*(Radius*2-d.magnitude)/2;var rel=Vector2.Dot(enemy.Velocity-player.Velocity,n);if(rel<0){player.Velocity+=n*rel*1.65f;enemy.Velocity-=n*rel*1.65f;Damage(player,enemy,5.5f+Mathf.Min(12,-rel*2.2f),"",balls[chosen].Color);Damage(enemy,player,5.5f+Mathf.Min(12,-rel*2.2f),"",balls[enemyType].Color);}}
        void Damage(BallState attacker,BallState target,float value,string label,Color c){if(target.Invincible>0)return;float actual=value*(attacker==null?1:(attacker==player?balls[chosen]:balls[enemyType]).Damage)/(target==player?balls[chosen]:balls[enemyType]).Defense;if(attacker!=null&&attacker.Armed){actual*=2.15f;attacker.Armed=false;}target.Hp-=actual;texts.Add(new FloatingText{P=target.Pos,Value=string.IsNullOrEmpty(label)?"-"+actual.ToString("0"):label,C=c,Life=.7f});Burst(target.Pos,c,10);}
        void FireShot(BallState owner,bool fromPlayer,BallDefinition b){var target=fromPlayer?enemy:player;var v=(target.Pos-owner.Pos).normalized*(b.Role==Role.Magic?8.2f:11.5f);shots.Add(new Shot{P=owner.Pos+v.normalized*Radius,V=v,FromPlayer=fromPlayer,Magic=b.Role==Role.Magic,Life=2.2f,Damage=b.Role==Role.Magic?4.6f:3.8f,C=b.Color});owner.RangedCooldown=b.Role==Role.Magic?1.9f:1.15f;}
        void UpdateShots(float dt){for(int i=shots.Count-1;i>=0;i--){var s=shots[i];s.P+=s.V*60*dt;s.Life-=dt;var t=s.FromPlayer?enemy:player;if(Vector2.Distance(s.P,t.Pos)<Radius+8){Damage(s.FromPlayer?player:enemy,t,s.Damage,"",s.C);s.Life=0;}if(s.Life<=0)shots.RemoveAt(i);else shots[i]=s;}}
        void UseSkill(bool fromPlayer){var who=fromPlayer?player:enemy;var target=fromPlayer?enemy:player;var b=balls[fromPlayer?chosen:enemyType];if(who.Energy<100)return;who.Energy=0;switch(b.Ability){case Ability.Blast:who.Armed=true;who.Boost=2.6f;break;case Ability.Freeze:fields.Add(new Field{Ability=Ability.Freeze,FromPlayer=fromPlayer,P=target.Pos,Life=.85f,Radius=82});break;case Ability.Dash:who.Velocity=(target.Pos-who.Pos).normalized*MaxSpeed;who.Boost=1.9f;break;case Ability.Web:fields.Add(new Field{Ability=Ability.Web,FromPlayer=fromPlayer,P=new Vector2(0,who.Pos.y),Life=5.5f,Radius=38});break;case Ability.Shadow:who.Pos=target.Pos-(target.Pos-who.Pos).normalized*70;target.Poison=4.5f;break;case Ability.Knock:fields.Add(new Field{Ability=Ability.Knock,FromPlayer=fromPlayer,P=who.Pos,Life=2.4f,Radius=52});break;case Ability.Shield:who.Invincible=2;FireShot(who,fromPlayer,b);break;case Ability.BlackHole:fields.Add(new Field{Ability=Ability.BlackHole,FromPlayer=fromPlayer,P=who.Pos,Life=3.5f,Radius=235});break;}Burst(who.Pos,b.Color,25);}
        void UpdateFields(float dt){for(int i=fields.Count-1;i>=0;i--){var f=fields[i];f.Life-=dt;f.Tick+=dt;var target=f.FromPlayer?enemy:player;if(f.Ability==Ability.BlackHole){var d=f.P-target.Pos;if(d.magnitude<f.Radius)target.Velocity+=d.normalized*(.55f+1.75f*(1-d.magnitude/f.Radius))*dt;}else if(f.Ability==Ability.Web&&Mathf.Abs(target.Pos.y-f.P.y)<f.Radius){target.Slow=Mathf.Max(target.Slow,3.2f);if(f.Tick>.5f){Damage(null,target,3.2f,"WEB",Color.red);f.Tick=0;}}else if(f.Ability==Ability.Freeze&&f.Life<.12f&&f.Tick>0){target.Slow=2.8f;Damage(null,target,7.5f,"ICE",Color.cyan);f.Tick=-99;}else if(f.Ability==Ability.Knock){f.P+= (target.Pos-f.P).normalized*250*dt;if(Vector2.Distance(f.P,target.Pos)<Radius+f.Radius){target.Stun=1.65f;Damage(null,target,10,"STUN",Color.green);f.Life=0;}}if(f.Life<=0)fields.RemoveAt(i);else fields[i]=f;}}
        void Bot(float dt){botDecision-=dt;botSkillClock+=dt;if(botDecision>0)return;float distance=Vector2.Distance(player.Pos,enemy.Pos);bool useful=difficulty==2?distance<420:(difficulty==1&&distance<300&&UnityEngine.Random.value<.72f);if(enemy.Energy>=100&&botSkillClock>1.5f&&(difficulty==0?UnityEngine.Random.value<.35f:useful)){UseSkill(false);botSkillClock=0;}botDecision=difficulty==2?.12f:.5f;}
        void DrawFields(Rect r){foreach(var f in fields){var pos=r.position+f.P;if(f.Ability==Ability.Web)Line(r.position+new Vector2(4,f.P.y),r.position+new Vector2(ArenaW-4,f.P.y),Color.white,3);else{var c=f.Ability==Ability.BlackHole?new Color(.55f,.36f,.96f,.22f):new Color(.2f,.8f,1,.2f);Circle(pos,f.Radius,c);}}}
        void DrawBall(Vector2 p,float radius,BallDefinition b,BallState state){Circle(p,radius+12,new Color(b.Color.r,b.Color.g,b.Color.b,.16f));Circle(p,radius,Color.Lerp(b.Color,new Color(.06f,.08f,.13f),.25f));GUI.Label(new Rect(p.x-radius,p.y-radius-2,radius*2,radius*2),b.Icon,Style((int)(radius*.75f),FontStyle.Bold,Color.white,TextAnchor.MiddleCenter));if(state!=null&&state.Invincible>0)CircleOutline(p,radius+14,Color.white,4);}
        void Burst(Vector2 p,Color c,int count){for(int i=0;i<count;i++)particles.Add(new Particle{P=p,V=UnityEngine.Random.insideUnitCircle*3,C=c,Life=.5f,Size=3});}
        void Panel(Rect r,string c){GUI.DrawTexture(r,Tex(c));}
        void Bar(Rect r,float v,Color c){GUI.DrawTexture(r,Tex("#1a2338"));GUI.DrawTexture(new Rect(r.x,r.y,r.width*Mathf.Clamp01(v/100),r.height),Tex("#"+ColorUtility.ToHtmlStringRGB(c)));}
        void Circle(Vector2 p,float radius,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),disc);GUI.color=old;}
        void CircleOutline(Vector2 p,float radius,Color c,float w){Line(p+Vector2.left*radius,p+Vector2.right*radius,c,w);Line(p+Vector2.up*radius,p+Vector2.down*radius,c,w);}
        void Line(Vector2 a,Vector2 b,Color c,float w=1){var old=GUI.color;GUI.color=c;var d=b-a;var m=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a);GUI.DrawTexture(new Rect(a.x,a.y,d.magnitude,w),pixel);GUI.matrix=m;GUI.color=old;}
    }
}
