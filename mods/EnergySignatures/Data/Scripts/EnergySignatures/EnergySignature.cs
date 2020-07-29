using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace xirathonxbox.spaceengineers.mods
{
    public class EnergySignature
    {
        public Color DefaultColor = new Color(117, 201, 241);
        public List<EnergySignature> m_group = new List<EnergySignature>(10);
        public const double ClusterAngle = 10.0;
        public const int MaxTextLength = 64;
        public const double ClusterNearDistance = 3500.0;
        public const double ClusterScaleDistance = 20000.0;
        public const double MinimumTargetRange = 2000.0;
        public const double OreDistance = 200.0;
        private const double AngleConversion = 0.00872664625997165;
        private bool m_alwaysVisible;

        //TODO: this may need to be updated
        private float screenWidth = MyAPIGateway.Session?.Config?.ScreenWidth ?? MyAPIGateway.Session.Camera.ViewportSize.X;
        private float screenHeight = MyAPIGateway.Session?.Config?.ScreenHeight ?? MyAPIGateway.Session.Camera.ViewportSize.Y;

        public Vector3D WorldPosition { get; private set; }

        public EnergySignatureType POIType { get; private set; }

        public MyRelationsBetweenPlayerAndBlock Relationship { get; private set; }

        public IMyEntity Entity { get; private set; }

        public StringBuilder Text { get; private set; }

        public double Distance { get; private set; }

        public double DistanceToCam { get; private set; }

        public string ContainerRemainingTime { get; set; }

        public bool AlwaysVisible
        {
            get
            {
                return this.POIType == EnergySignatureType.Ore && this.Distance < 200.0 || this.m_alwaysVisible;
            }
            set
            {
                this.m_alwaysVisible = value;
            }
        }

        public bool AllowsCluster
        {
            get
            {
                return !this.AlwaysVisible && this.POIType != EnergySignatureType.Target && (this.POIType != EnergySignatureType.Ore || this.Distance >= 200.0);
            }
        }

        public EnergySignature()
        {
            this.WorldPosition = Vector3D.Zero;
            this.POIType = EnergySignatureType.Unknown;
            this.Relationship = MyRelationsBetweenPlayerAndBlock.Owner;
            this.Text = new StringBuilder(64, 64);
        }

        public override string ToString()
        {
            return this.POIType.ToString() + ": " + (object)this.Text + " (" + (object)this.Distance + ")";
        }

        public void Reset()
        {
            this.WorldPosition = Vector3D.Zero;
            this.POIType = EnergySignatureType.Unknown;
            this.Relationship = MyRelationsBetweenPlayerAndBlock.Owner;
            this.Entity = null;
            this.Text.Clear();
            this.m_group.Clear();
            this.Distance = 0.0;
            this.DistanceToCam = 0.0;
            this.AlwaysVisible = false;
            this.ContainerRemainingTime = (string)null;
        }

        public void SetState(Vector3D position, EnergySignatureType type, MyRelationsBetweenPlayerAndBlock relationship)
        {
            this.WorldPosition = position;
            this.POIType = type;
            this.Relationship = relationship;
            this.Distance = (position - this.GetDistanceMeasuringMatrix().Translation).Length();
            this.DistanceToCam = (this.WorldPosition - MyAPIGateway.Session.Camera.WorldMatrix.Translation).Length();
        }

        //TODO: Cache this value?!?
        private MatrixD GetDistanceMeasuringMatrix()
        {

            var localCharacterMatrix = MyAPIGateway.Session.LocalHumanPlayer?.Character != null ? new MatrixD?(MyAPIGateway.Session.LocalHumanPlayer.Character.WorldMatrix) : new MatrixD?();
            var controlledEntityMatrix = MyAPIGateway.Session.ControlledObject != null ? new MatrixD?(MyAPIGateway.Session.ControlledObject.Entity.PositionComp.WorldMatrixRef) : new MatrixD?();

            //if (MySession.Static.CameraOnCharacter && localCharacterMatrix.HasValue)
            if(MyAPIGateway.Session.ControlledObject == null && localCharacterMatrix.HasValue)
                return localCharacterMatrix.Value;
            else
                return controlledEntityMatrix.Value;
        }

        public void SetEntity(IMyEntity entity)
        {
            this.Entity = entity;
        }

        public void SetText(StringBuilder text)
        {
            this.Text.Clear();
            if (text == null)
                return;
            this.Text.AppendSubstring(text, 0, Math.Min(text.Length, 64));
        }

        public void SetText(string text)
        {
            this.Text.Clear();
            if (string.IsNullOrWhiteSpace(text))
                return;
            this.Text.Append(text, 0, Math.Min(text.Length, 64));
        }

        public bool AddPOI(EnergySignature poi)
        {
            if (this.POIType != EnergySignatureType.Group)
                return false;
            Vector3D vector3D = this.WorldPosition * (double)this.m_group.Count;
            this.m_group.Add(poi);
            this.Text.Clear();
            this.Text.Append(this.m_group.Count);
            switch (this.GetGroupRelation())
            {
                
                case MyRelationsBetweenPlayerAndBlock.NoOwnership:
                    this.Text.AppendStringBuilder(MyTexts.Get(MyStringId.GetOrCompute("Signal_Mixed")));
                    break;
                case MyRelationsBetweenPlayerAndBlock.Owner:
                    this.Text.AppendStringBuilder(MyTexts.Get(MyStringId.GetOrCompute("Signal_Own")));
                    break;
                case MyRelationsBetweenPlayerAndBlock.FactionShare:
                    this.Text.AppendStringBuilder(MyTexts.Get(MyStringId.GetOrCompute("Signal_Friendly")));
                    break;
                case MyRelationsBetweenPlayerAndBlock.Neutral:
                    this.Text.AppendStringBuilder(MyTexts.Get(MyStringId.GetOrCompute("Signal_Neutral")));
                    break;
                case MyRelationsBetweenPlayerAndBlock.Enemies:
                    this.Text.AppendStringBuilder(MyTexts.Get(MyStringId.GetOrCompute("Signal_Enemy")));
                    break;
            }
            this.WorldPosition = (vector3D + poi.WorldPosition) / (double)this.m_group.Count;
            this.Distance = (this.WorldPosition - this.GetDistanceMeasuringMatrix().Translation).Length();
            this.DistanceToCam = (this.WorldPosition - MyAPIGateway.Session.Camera.WorldMatrix.Translation).Length();
            if (poi.Relationship > this.Relationship)
                this.Relationship = poi.Relationship;
            return true;
        }

        public bool IsPOINearby(EnergySignature poi, Vector3D cameraPosition, double angle = 10.0)
        {
            Vector3D vector3D = 0.5 * (this.WorldPosition - poi.WorldPosition);
            double num1 = vector3D.LengthSquared();
            double num2 = (cameraPosition - (poi.WorldPosition + vector3D)).Length();
            double num3 = Math.Sin(angle * (Math.PI / 360.0)) * num2;
            double num4 = num3 * num3;
            return num1 <= num4;
        }

        public void GetColorAndFontForRelationship(MyRelationsBetweenPlayerAndBlock relationship, out Color color, out Color fontColor, out string font)
        {
            color = Color.White;
            fontColor = Color.White;
            font = "White";
            switch (relationship)
            {
                case MyRelationsBetweenPlayerAndBlock.Owner:
                    color = new Color(117, 201, 241);
                    fontColor = new Color(117, 201, 241);
                    font = "Blue";
                    break;
                case MyRelationsBetweenPlayerAndBlock.FactionShare:
                case MyRelationsBetweenPlayerAndBlock.Friends:
                    color = new Color(101, 178, 90);
                    font = "Green";
                    break;
                case MyRelationsBetweenPlayerAndBlock.Enemies:
                    color = new Color(227, 62, 63);
                    font = "Red";
                    break;
            }
        }

        public void GetPOIColorAndFontInformation(out Color poiColor, out Color fontColor, out string font)
        {
            poiColor = Color.White;
            fontColor = Color.White;
            font = "White";
            switch (this.POIType)
            {
                case EnergySignatureType.Unknown:
                    poiColor = Color.White;
                    font = "White";
                    fontColor = Color.White;
                    break;
                case EnergySignatureType.Group:
                    bool flag = true;
                    EnergySignatureType pointOfInterestType = EnergySignatureType.Unknown;
                    if (this.m_group.Count > 0)
                    {
                        this.m_group[0].GetPOIColorAndFontInformation(out poiColor, out fontColor, out font);
                        pointOfInterestType = this.m_group[0].POIType;
                    }
                    for (int index = 1; index < this.m_group.Count; ++index)
                    {
                        if (this.m_group[index].POIType != pointOfInterestType)
                        {
                            flag = false;
                            break;
                        }
                    }
                    if (flag)
                        break;
                    this.GetColorAndFontForRelationship(this.GetGroupRelation(), out poiColor, out fontColor, out font);
                    break;
                case EnergySignatureType.Ore:
                    poiColor = Color.Khaki;
                    font = "White";
                    fontColor = Color.Khaki;
                    break;
                case EnergySignatureType.GPS:
                case EnergySignatureType.ContractGPS:
                    poiColor = this.DefaultColor;
                    fontColor = this.DefaultColor;
                    font = "Blue";
                    break;
                case EnergySignatureType.Objective:
                    poiColor = this.DefaultColor * 1.3f;
                    fontColor = this.DefaultColor * 1.3f;
                    font = "Blue";
                    break;
                case EnergySignatureType.Scenario:
                    poiColor = Color.DarkOrange;
                    fontColor = Color.DarkOrange;
                    font = "White";
                    break;
                default:
                    this.GetColorAndFontForRelationship(this.Relationship, out poiColor, out fontColor, out font);
                    break;
            }
        }

        private MyRelationsBetweenPlayerAndBlock GetGroupRelation()
        {
            if (this.m_group == null || this.m_group.Count == 0)
                return MyRelationsBetweenPlayerAndBlock.NoOwnership;
            MyRelationsBetweenPlayerAndBlock betweenPlayerAndBlock = this.m_group[0].Relationship;
            for (int index = 1; index < this.m_group.Count; ++index)
            {
                if (this.m_group[index].Relationship != betweenPlayerAndBlock)
                {
                    if (betweenPlayerAndBlock == MyRelationsBetweenPlayerAndBlock.Owner && this.m_group[index].Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare)
                    {
                        betweenPlayerAndBlock = MyRelationsBetweenPlayerAndBlock.FactionShare;
                    }
                    else
                    {
                        if (betweenPlayerAndBlock != MyRelationsBetweenPlayerAndBlock.FactionShare || this.m_group[index].Relationship != MyRelationsBetweenPlayerAndBlock.Owner)
                            return MyRelationsBetweenPlayerAndBlock.NoOwnership;
                        betweenPlayerAndBlock = MyRelationsBetweenPlayerAndBlock.FactionShare;
                    }
                }
            }
            return betweenPlayerAndBlock == MyRelationsBetweenPlayerAndBlock.NoOwnership ? MyRelationsBetweenPlayerAndBlock.Neutral : betweenPlayerAndBlock;
        }

        public void Draw(MyHudMarkerRender renderer, float alphaMultiplierMarker = 1f, float alphaMultiplierText = 1f, float scale = 1f, bool drawBox = true)
        {
            Vector2 projectedPoint2D = Vector2.Zero;
            bool isBehind = false;
            if (!EnergySignature.TryComputeScreenPoint(this.WorldPosition, out projectedPoint2D, out isBehind))
                return;

            

            Vector2 vector2_1 = new Vector2(screenWidth, screenHeight);
            Vector2 hudSize = new Vector2(1f, screenHeight / screenWidth);
            Vector2 hudSizeHalf = hudSize / 2f;
            float num1 = vector2_1.Y / 1080f;
            Vector2 vector2_2 = projectedPoint2D * hudSize;
            Color white1 = Color.White;
            Color fontColor = Color.White;
            string font = "White";
            this.GetPOIColorAndFontInformation(out white1, out fontColor, out font);
            Vector2 upVector = vector2_2 - hudSizeHalf;
            Vector3D vector3D = Vector3D.Transform(this.WorldPosition, MyAPIGateway.Session.Camera.ViewMatrix);
            float num2 = 0.04f;
            if ((double)vector2_2.X < (double)num2 || (double)vector2_2.X > (double)hudSize.X - (double)num2 || ((double)vector2_2.Y < (double)num2 || (double)vector2_2.Y > (double)hudSize.Y - (double)num2) || vector3D.Z > 0.0)
            {
                if (this.POIType == EnergySignatureType.Target)
                    return;
                Vector2 vector2_3 = Vector2.Normalize(upVector);
                Vector2 position = hudSizeHalf + hudSizeHalf * vector2_3 * 0.77f;
                upVector = position - hudSizeHalf;
                if ((double)upVector.LengthSquared() > 9.99999943962493E-11)
                    upVector.Normalize();
                else
                    upVector = new Vector2(1f, 0.0f);
                float num3 = 0.0053336f / num1 / num1;
                renderer.AddTexturedQuad("HudAtlas0.dds_DirectionIndicator", position, upVector, white1, num3, num3);
                vector2_2 = position - upVector * 0.006667f * 2f;
            }
            else
            {
                float num3 = scale * 0.006667f / num1 / num1;
                if (this.POIType == EnergySignatureType.Target)
                {
                    renderer.AddTexturedQuad("HudAtlas0.dds_TargetTurret", vector2_2, -Vector2.UnitY, Color.White, num3, num3);
                    return;
                }
                if (drawBox)
                    renderer.AddTexturedQuad("HudAtlas0.dds_Target_neutral", vector2_2, -Vector2.UnitY, white1, num3, num3);
            }
            float num4 = 0.03f;
            float num5 = 0.07f;
            float num6 = 0.15f;
            float num7 = upVector.Length();
            float val;
            float num8;
            int num9;
            if ((double)num7 <= (double)num4)
            {
                val = 1f;
                num8 = 1f;
                num9 = 0;
            }
            else if ((double)num7 > (double)num4 && (double)num7 < (double)num5)
            {
                float num3 = num6 - num4;
                float num10 = (float)(1.0 - ((double)num7 - (double)num4) / (double)num3);
                val = num10 * num10;
                float num11 = num5 - num4;
                float num12 = (float)(1.0 - ((double)num7 - (double)num4) / (double)num11);
                num8 = num12 * num12;
                num9 = 1;
            }
            else if ((double)num7 >= (double)num5 && (double)num7 < (double)num6)
            {
                float num3 = num6 - num4;
                float num10 = (float)(1.0 - ((double)num7 - (double)num4) / (double)num3);
                val = num10 * num10;
                float num11 = num6 - num5;
                float num12 = (float)(1.0 - ((double)num7 - (double)num5) / (double)num11);
                num8 = num12 * num12;
                num9 = 2;
            }
            else
            {
                val = 0.0f;
                num8 = 0.0f;
                num9 = 2;
            }
            float num13 = MathHelper.Clamp((float)(((double)num7 - 0.200000002980232) / 0.5), 0.0f, 1f);
            float num14 = MyMath.Clamp(val, 0.0f, 1f);
            //if (MyHudMarkerRender.m_disableFading || MyHudMarkerRender.SignalDisplayMode == MyHudMarkerRender.SignalMode.FullDisplay || this.AlwaysVisible)
            {
                num14 = 1f;
                num8 = 1f;
                num13 = 1f;
                num9 = 0;
            }

            Vector2 vector2_4 = new Vector2(0.0f, (float)((double)scale * (double)num1 * 24.0) / (float)screenWidth);
            //if ((MyHudMarkerRender.SignalDisplayMode != MyHudMarkerRender.SignalMode.NoNames || this.POIType == EnergySignatureType.ButtonMarker || (MyHudMarkerRender.m_disableFading || this.AlwaysVisible)) && ((double)num14 > 1.40129846432482E-45 && this.Text.Length > 0))
            if ((double)num14 > 1.40129846432482E-45 && this.Text.Length > 0)
            {
                MyHudText myHudText = renderer.m_hudScreen.AllocateText();
                if (myHudText != null)
                {
                    fontColor.A = (byte)((double)byte.MaxValue * (double)alphaMultiplierText * (double)num14);
                    myHudText.Start(font, vector2_2 - vector2_4, fontColor, 0.7f / num1, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                    myHudText.Append(this.Text);
                }
            }

            if (this.POIType != EnergySignatureType.Group)
            {
                byte a = white1.A;
                white1.A = (byte)((double)byte.MaxValue * (double)alphaMultiplierMarker * (double)num13);
                EnergySignature.DrawIcon(renderer, this.POIType, this.Relationship, vector2_2, white1, scale);
                white1.A = a;
                MyHudText myHudText1 = renderer.m_hudScreen.AllocateText();
                if (myHudText1 != null)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    MyHudMarkerRender.AppendDistance(stringBuilder, this.Distance);
                    fontColor.A = (byte)((double)alphaMultiplierText * (double)byte.MaxValue);
                    myHudText1.Start(font, vector2_2 + vector2_4 * (float)(0.699999988079071 + 0.300000011920929 * (double)num14), fontColor, (float)(0.5 + 0.200000002980232 * (double)num14) / num1, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                    myHudText1.Append(stringBuilder);
                }
                if (string.IsNullOrEmpty(this.ContainerRemainingTime))
                    return;

                MyHudText myHudText2 = renderer.m_hudScreen.AllocateText();
                myHudText2.Start(font, vector2_2 + vector2_4 * (float)(1.60000002384186 + 0.300000011920929 * (double)num14), fontColor, (float)(0.5 + 0.200000002980232 * (double)num14) / num1, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                myHudText2.Append(this.ContainerRemainingTime);
            }
            else
            {
                Dictionary<MyRelationsBetweenPlayerAndBlock, List<EnergySignature>> significantGroupPoIs = this.GetSignificantGroupPOIs();
                Vector2[] vector2Array1 = new Vector2[5]
                {
            new Vector2(-6f, -4f),
            new Vector2(6f, -4f),
            new Vector2(-6f, 4f),
            new Vector2(6f, 4f),
            new Vector2(0.0f, 12f)
                };
                Vector2[] vector2Array2 = new Vector2[5]
                {
            new Vector2(16f, -4f),
            new Vector2(16f, 4f),
            new Vector2(16f, 12f),
            new Vector2(16f, 20f),
            new Vector2(16f, 28f)
                };
                for (int index = 0; index < vector2Array1.Length; ++index)
                {
                    float num3 = num9 < 2 ? 1f : num8;
                    float y1 = vector2Array1[index].Y;
                    vector2Array1[index].X = (vector2Array1[index].X + 22f * num3) / (float)screenWidth;
                    vector2Array1[index].Y = y1 / 1080f / num1;
                    
                    //can't support triplehead
                    //if (MyVideoSettingsManager.IsTripleHead())
                    //    vector2Array1[index].X /= 0.33f;
                    if ((double)vector2Array1[index].Y <= 1.40129846432482E-45)
                        vector2Array1[index].Y = y1 / 1080f;
                    float y2 = vector2Array2[index].Y;
                    vector2Array2[index].X = vector2Array2[index].X / (float)screenWidth / num1;
                    vector2Array2[index].Y = y2 / 1080f / num1;
                    //can't support triplehead
                    //if (MyVideoSettingsManager.IsTripleHead())
                    //    vector2Array2[index].X /= 0.33f;
                    if ((double)vector2Array2[index].Y <= 1.40129846432482E-45)
                        vector2Array2[index].Y = y2 / 1080f;
                }
                int index1 = 0;
                if (significantGroupPoIs.Count > 1)
                {
                    MyRelationsBetweenPlayerAndBlock[] betweenPlayerAndBlockArray = new MyRelationsBetweenPlayerAndBlock[4]
                    {
              MyRelationsBetweenPlayerAndBlock.Owner,
              MyRelationsBetweenPlayerAndBlock.FactionShare,
              MyRelationsBetweenPlayerAndBlock.Neutral,
              MyRelationsBetweenPlayerAndBlock.Enemies
                    };
                    foreach (MyRelationsBetweenPlayerAndBlock index2 in betweenPlayerAndBlockArray)
                    {
                        if (significantGroupPoIs.ContainsKey(index2))
                        {
                            List<EnergySignature> pointOfInterestList = significantGroupPoIs[index2];
                            if (pointOfInterestList.Count != 0)
                            {
                                EnergySignature poi = pointOfInterestList[0];
                                if (poi != null)
                                {
                                    if (poi.POIType == EnergySignatureType.ContractGPS)
                                        poi.GetPOIColorAndFontInformation(out white1, out fontColor, out font);
                                    else
                                        this.GetColorAndFontForRelationship(index2, out white1, out fontColor, out font);
                                    float amount = num9 == 0 ? 1f : num8;
                                    if (num9 >= 2)
                                        amount = 0.0f;
                                    Vector2 vector2_3 = Vector2.Lerp(vector2Array1[index1], vector2Array2[index1], amount);
                                    string iconForRelationship = EnergySignature.GetIconForRelationship(index2);
                                    white1.A = (byte)((double)alphaMultiplierMarker * (double)white1.A);
                                    EnergySignature.DrawIcon(renderer, iconForRelationship, vector2_2 + vector2_3, white1, 0.75f / num1);
                                    if (this.IsPoiAtHighAlert(poi))
                                    {
                                        Color white2 = Color.White;
                                        white2.A = (byte)((double)alphaMultiplierMarker * (double)byte.MaxValue);
                                        EnergySignature.DrawIcon(renderer, "Textures\\HUD\\marker_alert.dds", vector2_2 + vector2_3, white2, 0.75f / num1);
                                    }
                                    //if ((MyHudMarkerRender.SignalDisplayMode != MyHudMarkerRender.SignalMode.NoNames || MyHudMarkerRender.m_disableFading || this.AlwaysVisible) && poi.Text.Length > 0)
                                    if (poi.Text.Length > 0)
                                    {
                                        MyHudText myHudText = renderer.m_hudScreen.AllocateText();
                                        if (myHudText != null)
                                        {
                                            float num3 = 1f;
                                            if (num9 == 1)
                                                num3 = num8;
                                            else if (num9 > 1)
                                                num3 = 0.0f;
                                            fontColor.A = (byte)((double)byte.MaxValue * (double)alphaMultiplierText * (double)num3);
                                            Vector2 vector2_5 = new Vector2(8f / (float)screenWidth, 0.0f);
                                            vector2_5.X /= num1;
                                            myHudText.Start(font, vector2_2 + vector2_3 + vector2_5, fontColor, 0.55f / num1, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
                                            myHudText.Append(poi.Text);
                                        }
                                    }
                                    ++index1;
                                }
                            }
                        }
                    }
                }
                else
                {
                    using (Dictionary<MyRelationsBetweenPlayerAndBlock, List<EnergySignature>>.Enumerator enumerator = significantGroupPoIs.GetEnumerator())
                    {
                    label_86:
                        while (enumerator.MoveNext())
                        {
                            KeyValuePair<MyRelationsBetweenPlayerAndBlock, List<EnergySignature>> current = enumerator.Current;
                            MyRelationsBetweenPlayerAndBlock key = current.Key;
                            if (significantGroupPoIs.ContainsKey(key))
                            {
                                List<EnergySignature> pointOfInterestList = current.Value;
                                int index2 = 0;
                                while (true)
                                {
                                    if (index2 < 4 && index2 < pointOfInterestList.Count)
                                    {
                                        EnergySignature poi = pointOfInterestList[index2];
                                        if (poi != null)
                                        {
                                            if (poi.POIType == EnergySignatureType.Scenario || poi.POIType == EnergySignatureType.ContractGPS || poi.POIType == EnergySignatureType.Ore)
                                                poi.GetPOIColorAndFontInformation(out white1, out fontColor, out font);
                                            else
                                                this.GetColorAndFontForRelationship(key, out white1, out fontColor, out font);
                                            float amount = num9 == 0 ? 1f : num8;
                                            if (num9 >= 2)
                                                amount = 0.0f;
                                            Vector2 vector2_3 = Vector2.Lerp(vector2Array1[index1], vector2Array2[index1], amount);
                                            string centerIconSprite = poi.POIType != EnergySignatureType.Scenario ? EnergySignature.GetIconForRelationship(key) : "Textures\\HUD\\marker_scenario.dds";
                                            white1.A = (byte)((double)alphaMultiplierMarker * (double)white1.A);
                                            EnergySignature.DrawIcon(renderer, centerIconSprite, vector2_2 + vector2_3, white1, 0.75f / num1);
                                            if (this.ShouldDrawHighAlertMark(poi))
                                            {
                                                Color white2 = Color.White;
                                                white2.A = (byte)((double)alphaMultiplierMarker * (double)byte.MaxValue);
                                                EnergySignature.DrawIcon(renderer, "Textures\\HUD\\marker_alert.dds", vector2_2 + vector2_3, white2, 0.75f / num1);
                                            }

                                            //if ((MyHudMarkerRender.SignalDisplayMode != MyHudMarkerRender.SignalMode.NoNames || MyHudMarkerRender.m_disableFading || this.AlwaysVisible) && poi.Text.Length > 0)
                                            if (poi.Text.Length > 0)
                                            {
                                                MyHudText myHudText = renderer.m_hudScreen.AllocateText();
                                                if (myHudText != null)
                                                {
                                                    float num3 = 1f;
                                                    if (num9 == 1)
                                                        num3 = num8;
                                                    else if (num9 > 1)
                                                        num3 = 0.0f;
                                                    fontColor.A = (byte)((double)byte.MaxValue * (double)alphaMultiplierText * (double)num3);
                                                    Vector2 vector2_5 = new Vector2(8f / (float)screenWidth, 0.0f);
                                                    vector2_5.X /= num1;
                                                    myHudText.Start(font, vector2_2 + vector2_3 + vector2_5, fontColor, 0.55f / num1, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
                                                    myHudText.Append(poi.Text);
                                                }
                                            }
                                            ++index1;
                                        }
                                        ++index2;
                                    }
                                    else
                                        goto label_86;
                                }
                            }
                        }
                    }
                }
                this.GetPOIColorAndFontInformation(out white1, out fontColor, out font);
                float amount1 = num9 == 0 ? 1f : num8;
                if (num9 >= 2)
                    amount1 = 0.0f;
                Vector2 vector2_6 = Vector2.Lerp(vector2Array1[4], vector2Array2[index1], amount1);
                Vector2 vector2_7 = Vector2.Lerp(Vector2.Zero, new Vector2(0.02222222f / num1, 1f / 270f / num1), amount1);
                MyHudText myHudText1 = renderer.m_hudScreen.AllocateText();
                if (myHudText1 == null)
                    return;
                StringBuilder stringBuilder = new StringBuilder();
                MyHudMarkerRender.AppendDistance(stringBuilder, this.Distance);
                fontColor.A = (byte)((double)alphaMultiplierText * (double)byte.MaxValue);
                myHudText1.Start(font, vector2_2 + vector2_6 + vector2_7, fontColor, (float)(0.5 + 0.200000002980232 * (double)num14) / num1, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                myHudText1.Append(stringBuilder);
            }
        }

        private Dictionary<MyRelationsBetweenPlayerAndBlock, List<EnergySignature>> GetSignificantGroupPOIs()
        {
            Dictionary<MyRelationsBetweenPlayerAndBlock, List<EnergySignature>> dictionary = new Dictionary<MyRelationsBetweenPlayerAndBlock, List<EnergySignature>>();
            if (this.m_group == null || this.m_group.Count == 0)
                return dictionary;
            bool flag = true;
            MyRelationsBetweenPlayerAndBlock relationship = this.m_group[0].Relationship;
            for (int index = 1; index < this.m_group.Count; ++index)
            {
                if (this.m_group[index].Relationship != relationship)
                {
                    flag = false;
                    break;
                }
            }
            if (flag)
            {
                this.m_group.Sort(new Comparison<EnergySignature>(this.ComparePointOfInterest));
                dictionary[relationship] = new List<EnergySignature>();
                for (int index = this.m_group.Count - 1; index >= 0; --index)
                {
                    dictionary[relationship].Add(this.m_group[index]);
                    if (dictionary[relationship].Count >= 4)
                        break;
                }
            }
            else
            {
                for (int index = 0; index < this.m_group.Count; ++index)
                {
                    EnergySignature poiA = this.m_group[index];
                    MyRelationsBetweenPlayerAndBlock key = poiA.Relationship;
                    if (key == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                        key = MyRelationsBetweenPlayerAndBlock.Neutral;
                    if (dictionary.ContainsKey(key))
                    {
                        if (this.ComparePointOfInterest(poiA, dictionary[key][0]) > 0)
                        {
                            dictionary[key].Clear();
                            dictionary[key].Add(poiA);
                        }
                    }
                    else
                    {
                        dictionary[key] = new List<EnergySignature>();
                        dictionary[key].Add(poiA);
                    }
                }
            }
            return dictionary;
        }

        private bool IsRelationHostile(MyRelationsBetweenPlayerAndBlock relationshipA, MyRelationsBetweenPlayerAndBlock relationshipB)
        {
            if (relationshipA == MyRelationsBetweenPlayerAndBlock.Owner || relationshipA == MyRelationsBetweenPlayerAndBlock.FactionShare)
                return relationshipB == MyRelationsBetweenPlayerAndBlock.Enemies;
            return (relationshipB == MyRelationsBetweenPlayerAndBlock.Owner || relationshipB == MyRelationsBetweenPlayerAndBlock.FactionShare) && relationshipA == MyRelationsBetweenPlayerAndBlock.Enemies;
        }

        private bool IsPoiAtHighAlert(EnergySignature poi)
        {
            if (poi.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral)
                return false;
            if (poi.POIType == EnergySignatureType.Scenario)
                return true;
            foreach (EnergySignature pointOfInterest in this.m_group)
            {
                if (this.IsRelationHostile(poi.Relationship, pointOfInterest.Relationship) && (double)((Vector3)(pointOfInterest.WorldPosition - poi.WorldPosition)).LengthSquared() < 1000000.0)
                    return true;
            }
            return false;
        }

        private bool ShouldDrawHighAlertMark(EnergySignature poi)
        {
            return poi.POIType != EnergySignatureType.Scenario && this.IsPoiAtHighAlert(poi);
        }

        private bool IsGrid()
        {
            return this.POIType == EnergySignatureType.SmallEntity || this.POIType == EnergySignatureType.LargeEntity || this.POIType == EnergySignatureType.StaticEntity;
        }

        private static void DrawIcon(
          MyHudMarkerRender renderer,
          EnergySignatureType poiType,
          MyRelationsBetweenPlayerAndBlock relationship,
          Vector2 screenPosition,
          Color markerColor,
          float sizeScale = 1f)
        {
            string empty = string.Empty;
            Vector2 vector2_1 = new Vector2(12f, 12f);
            string texture;
            switch (poiType)
            {
                case EnergySignatureType.Unknown:
                case EnergySignatureType.UnknownEntity:
                case EnergySignatureType.Character:
                case EnergySignatureType.SmallEntity:
                case EnergySignatureType.LargeEntity:
                case EnergySignatureType.StaticEntity:
                    string iconForRelationship = EnergySignature.GetIconForRelationship(relationship);
                    EnergySignature.DrawIcon(renderer, iconForRelationship, screenPosition, markerColor, sizeScale);
                    return;
                case EnergySignatureType.Target:
                    texture = "HudAtlas0.dds_TargetTurret";
                    break;
                case EnergySignatureType.Group:
                    return;
                case EnergySignatureType.Ore:
                    texture = "HudAtlas0.dds_HudOre";
                    markerColor = Color.Khaki;
                    break;
                case EnergySignatureType.Hack:
                    texture = "HudAtlas0.dds_hit_confirmation";
                    break;
                case EnergySignatureType.GPS:
                case EnergySignatureType.Objective:
                    string centerIconSprite1 = "Textures\\HUD\\marker_gps.dds";
                    EnergySignature.DrawIcon(renderer, centerIconSprite1, screenPosition, markerColor, sizeScale);
                    return;
                case EnergySignatureType.ButtonMarker:
                    return;
                case EnergySignatureType.Scenario:
                    string centerIconSprite2 = "Textures\\HUD\\marker_scenario.dds";
                    EnergySignature.DrawIcon(renderer, centerIconSprite2, screenPosition, markerColor, sizeScale);
                    return;
                default:
                    return;
            }
            if (!string.IsNullOrWhiteSpace(empty))
            {
                Vector2 vector2_2 = vector2_1 * sizeScale;
                renderer.AddTexturedQuad(empty, screenPosition, -Vector2.UnitY, markerColor, vector2_2.X, vector2_2.Y);
            }
            else
            {
                float num = 0.0053336f * sizeScale;
                renderer.AddTexturedQuad(texture, screenPosition, -Vector2.UnitY, markerColor, num, num);
            }
        }

        public static string GetIconForRelationship(MyRelationsBetweenPlayerAndBlock relationship)
        {
            string str = string.Empty;
            switch (relationship)
            {
                case MyRelationsBetweenPlayerAndBlock.NoOwnership:
                case MyRelationsBetweenPlayerAndBlock.Neutral:
                    str = "Textures\\HUD\\marker_neutral.dds";
                    break;
                case MyRelationsBetweenPlayerAndBlock.Owner:
                    str = "Textures\\HUD\\marker_self.dds";
                    break;
                case MyRelationsBetweenPlayerAndBlock.FactionShare:
                case MyRelationsBetweenPlayerAndBlock.Friends:
                    str = "Textures\\HUD\\marker_friendly.dds";
                    break;
                case MyRelationsBetweenPlayerAndBlock.Enemies:
                    str = "Textures\\HUD\\marker_enemy.dds";
                    break;
            }
            return str;
        }

        private static void DrawIcon(
          MyHudMarkerRender renderer,
          string centerIconSprite,
          Vector2 screenPosition,
          Color markerColor,
          float sizeScale = 1f)
        {
            Vector2 vector2 = new Vector2(8f, 8f) * sizeScale;
            renderer.AddTexturedQuad(centerIconSprite, screenPosition, -Vector2.UnitY, markerColor, vector2.X, vector2.Y);
        }

        public static bool TryComputeScreenPoint(
          Vector3D worldPosition,
          out Vector2 projectedPoint2D,
          out bool isBehind)
        {
            Vector3D position = Vector3D.Transform(worldPosition, MyAPIGateway.Session.Camera.ViewMatrix);
            Vector4D vector4D = Vector4D.Transform(position, MyAPIGateway.Session.Camera.ProjectionMatrix);
            if (position.Z > 0.0)
            {
                vector4D.X *= -1.0;
                vector4D.Y *= -1.0;
            }
            if (vector4D.W == 0.0)
            {
                projectedPoint2D = Vector2.Zero;
                isBehind = false;
                return false;
            }
            projectedPoint2D = new Vector2((float)(vector4D.X / vector4D.W / 2.0) + 0.5f, (float)(-vector4D.Y / vector4D.W / 2.0 + 0.5));
            //can't support triplehead
            //if (MyVideoSettingsManager.IsTripleHead())
            //    projectedPoint2D.X = (float)(((double)projectedPoint2D.X - 0.333333343267441) / 0.333333343267441);

            Vector3D vector2 = worldPosition - MyAPIGateway.Session.Camera.WorldMatrix.Translation;
            vector2.Normalize();
            double num = Vector3D.Dot((Vector3D)MyAPIGateway.Session.Camera.ViewMatrix.Forward, vector2);
            isBehind = num < 0.0;
            return true;
        }

        private int ComparePointOfInterest(
          EnergySignature poiA,
          EnergySignature poiB)
        {
            int num1 = this.IsPoiAtHighAlert(poiA).CompareTo(this.IsPoiAtHighAlert(poiB));
            if (num1 != 0)
                return num1;
            if (poiA.POIType >= EnergySignatureType.UnknownEntity && poiB.POIType >= EnergySignatureType.UnknownEntity)
            {
                int num2 = poiA.POIType.CompareTo((object)poiB.POIType);
                if (num2 != 0)
                    return num2;
            }
            if (poiA.IsGrid() && poiB.IsGrid())
            {
                MyCubeBlock entity1 = poiA.Entity as MyCubeBlock;
                MyCubeBlock entity2 = poiB.Entity as MyCubeBlock;
                if (entity1 != null && entity2 != null)
                {
                    int num2 = entity1.CubeGrid.BlocksCount.CompareTo(entity2.CubeGrid.BlocksCount);
                    if (num2 != 0)
                        return num2;
                }
            }
            return poiB.Distance.CompareTo(poiA.Distance);
        }

        

        
    }

    public enum EnergySignatureType
    {
        Unknown,
        Target,
        Group,
        Ore,
        Hack,
        UnknownEntity,
        Character,
        SmallEntity,
        LargeEntity,
        StaticEntity,
        GPS,
        ButtonMarker,
        Objective,
        Scenario,
        ContractGPS,
    }

    public enum EnergySignatureState
    {
        NonDirectional,
        Directional,
    }
}
