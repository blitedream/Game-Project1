from pathlib import Path
p=Path('Assets/Scripts/Level2SinkholeBootstrap.cs')
s=p.read_text(encoding='utf-8-sig').replace('PreviewVersion = 25','PreviewVersion = 26')
s=s.replace('movement.ApplyLevel1ControlProfile();','movement.ApplyLevel1ControlProfile();\n        movement.ApplyLevel2Pace();')
a=s.index('        CreateGearPart(gearRoot.transform, PrimitiveType.Cylinder, "Left Tank"')
b=s.index('        DivingGearPickup pickup',a)
s=s[:a]+'''        var model = Resources.Load<GameObject>("Equipment/Level1DivingGear");
        if (model == null) throw new InvalidOperationException("Missing Level1 diving gear prefab");
        var visual = UnityEngine.Object.Instantiate(model, gearRoot.transform, false);
        visual.name = "Level1 Diving Gear Model";
        visual.transform.localScale = Vector3.one * .5f;
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position += Vector3.up * (gearPosition.y - bounds.min.y);
        }

'''+s[b:]
p.write_text(s,encoding='utf-8')
p=Path('Assets/Editor/Level2RepairValidation.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('baseline.ApplyLevel1ControlProfile();','baseline.ApplyLevel1ControlProfile();\n            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Level2") baseline.ApplyLevel2Pace();')
s=s.replace('first-level movement profile;','slower Level2 pace; solid shore ring; Level1 equipment;')
s=s.replace('        var stage=typeof(Level2MissionController)', '''        Require(pickup.transform.Find("Level1 Diving Gear Model") != null,"Not using Level1 equipment");
        Vector3 center=Level2SinkholeBootstrap.FusedWaterCenter;
        for(float radius=12.3f;radius<=23.5f;radius+=.4f) for(int angle=0;angle<360;angle+=2)
        {
            Vector3 p=center+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;
            Require(Level2SinkholeRuntimeLoader.TryFindVisibleCaveGround(p+Vector3.up*20,30,out var ground),"Shore hole: "+p);
            Require(ground.point.y>=center.y+.45f,"Shore sinks below water: "+p);
        }
        var stage=typeof(Level2MissionController)''')
p.write_text(s,encoding='utf-8')
