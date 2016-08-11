﻿using System;
using GameLogic.Utility;
using UnityEngine.SceneManagement;
using UnityEngine;
using Proto;
using UGameTools;

public class UReplayGate:UGate
{
    public UReplayGate(byte[] replayerData,int mapID)
    {
        var replayer = new GameLogic.Utility.NotifyMessagePool();
        replayer.LoadFormBytes(replayerData);
        Replayer = replayer;
        var data = ExcelConfig.ExcelToJSONConfigManager.Current.GetConfigByID<ExcelConfig.LevelData>(mapID);
        level = data.LevelResouceName;
    }
    private string level;
    private NotifyMessagePool Replayer;

    private AsyncOperation operation;

    #region implemented abstract members of UGate
    public override void JoinGate()
    {
        UUIManager.Singleton.ShowMask (true);
        UUIManager.Singleton.ShowLoading (0);
        operation = SceneManager.LoadSceneAsync (level, LoadSceneMode.Single);
        UUIManager.Singleton.HideAll();
    }
    public override void ExitGate()
    {
        //throw new NotImplementedException();
    }

    private float startTime = 0f;
    public override void Tick()
    {
        if (operation != null)
        {
            if (!operation.isDone)
            {
                UUIManager.Singleton.ShowLoading(operation.progress);
                return;
            }
            operation = null;
          
            UUIManager.Singleton.ShowMask(false);
            startTime = GetTime().Time;
        }

        if (frame == null)
        {
            Replayer.NextFrame(out frame);
        }

        if (frame != null)
        {
            if (frame.time > GetTime().Time - startTime)
                return;
            foreach (var i in frame.GetNotify())
                Process(i);
            frame = null;
        }
    }

    private GameLogic.Utility.NotifyMessagePool.Frame frame;


    public override EngineCore.Simulater.GTime GetTime()
    {
        return new EngineCore.Simulater.GTime(Time.time, Time.deltaTime);
    }
    #endregion

    private  System.Collections.Generic.Dictionary<long,UElementView> views = new System.Collections.Generic.Dictionary<long, UElementView>();
     

    public void Process(Proto.ISerializerable notify)
    {
        if (notify is Proto.Notify_CreateBattleCharacter)
        {
            var createcharacter = notify as Notify_CreateBattleCharacter;
            var resources = ExcelConfig.ExcelToJSONConfigManager.Current.GetConfigByID<ExcelConfig.CharacterData>(createcharacter.ConfigID);
            var view = UPerceptionView.Singleton.CreateBattleCharacterView(
                           resources.ResourcesPath,
                           createcharacter.Position.ToGVer3(), 
                           createcharacter.Forward.ToGVer3()) as UCharacterView;
            view.Index = createcharacter.Index;
            views.Add(view.Index, view);
        }
        else if (notify is Proto.Notify_CreateReleaser)
        {
            var creater = notify as Notify_CreateReleaser;
            var releaer = views[creater.ReleaserIndex] as UCharacterView;
            var target = views[creater.TargetIndex] as UCharacterView;
            var viewer = UPerceptionView.Singleton.CreateReleaserView(releaer, target, null) as UMagicReleaserView;
            viewer.SetCharacter(releaer, target);
            viewer.Index = creater.Index;
            views.Add(viewer.Index, viewer);
        }
        else if (notify is Proto.Notify_CreateMissile)
        {
            var create = notify as Notify_CreateMissile;
            var releaser = views[create.ReleaserIndex] as UMagicReleaserView;
            var layout = new Layout.LayoutElements.MissileLayout
            {
                fromBone = create.formBone,
                toBone = create.toBone,
                offset = create.offset.ToLVer3(),
                resourcesPath = create.ResourcesPath,
                speed = create.Speed
            };
            var view = UPerceptionView.Singleton.CreateMissile(releaser, layout) as UBattleMissileView;
            view.Index = create.Index;
            views.Add(view.Index, view);
        }
        else if (notify is Proto.Notify_LayoutPlayParticle)
        {
            var particle = notify as Notify_LayoutPlayParticle;
            var layout = new Layout.LayoutElements.ParticleLayout
                {
                    path = particle.Path ,
                    destoryTime = particle.DestoryTime,
                    destoryType = (Layout.LayoutElements.ParticleDestoryType)particle.DestoryType,
                    fromBoneName = particle.FromBoneName,
                    fromTarget = (Layout.TargetType)particle.FromTarget,
                    toBoneName = particle.ToBoneName,
                    toTarget = (Layout.TargetType)particle.ToTarget,
                    Bind = particle.Bind
                };
            var releaser = views[particle.ReleaseIndex] as UMagicReleaserView;
            UPerceptionView.Singleton.CreateParticlePlayer(releaser, layout);
        }
        else if (notify is Notify_LookAtCharacter)
        {
            var look = notify as Notify_LookAtCharacter;
            var owner = views[look.Own] as UCharacterView;
            var target = views[look.Target]as UCharacterView;
            owner.LookAt(target.Transform);
        }
        else if (notify is Proto.Notify_CharacterBeginMove)
        {
            var beginMove = notify as Notify_CharacterBeginMove;
            var view = views[beginMove.Index] as UCharacterView;
            view.MoveTo(beginMove.TargetPosition.ToGVer3());
            view.SetSpeed(beginMove.Speed);
        }
        else if (notify is Proto.Notify_CharacterStopMove)
        {
            var stop = notify as Notify_CharacterStopMove;
            var view = views[stop.Index] as UCharacterView;
            view.StopMove();
        }
        else if (notify is Proto.Notify_LayoutPlayMotion)
        {
            var motion = notify as Notify_LayoutPlayMotion;
            var view = views[motion.Index] as UCharacterView;
            view.PlayMotion(motion.Motion);
        }
        else if (notify is Proto.Notity_EffectAddHP)
        {
            var addHp = notify as Proto.Notity_EffectAddHP;
            var view = views[addHp.Index] as UCharacterView;
            view.ShowHPChange(addHp.CureHP,addHp.TargetHP,addHp.Max);
        }
        else if (notify is Proto.Notity_EffectSubHP)
        {
            var subHP = notify as Proto.Notity_EffectSubHP;
            var view = views[subHP.Index] as UCharacterView;
            view.ShowHPChange(-subHP.LostHP,subHP.TargetHP,subHP.Max);
            if (subHP.TargetHP == 0)
            {
                view.Death();
            }
        }
        else if (notify is Proto.Notify_ElementExitState)
        {
            var exit = notify as Notify_ElementExitState;
            var view = views[exit.Index];
            views.Remove(exit.Index);
            GameObject.Destroy(view.gameObject);
        }
        else if (notify is Proto.Notify_ElementJoinState)
        {
           
        }
        else
        {
            Debug.LogError("NO Handle:" + notify.GetType());
        }
    }
}