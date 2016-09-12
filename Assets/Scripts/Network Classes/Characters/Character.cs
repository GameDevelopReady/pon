﻿using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// Class for managing a character. Characters are objects that represent a player.
/// </summary>
public abstract class Character : NetworkEntity
{
    /// <summary>
    /// The player object controlling this character.
    /// </summary>
    public Player player;

    [SyncVar(hook = "OnUpdatePlayerId")]
    public NetworkInstanceId player_id;

    /// <summary>
    /// Whether this character is visible by the look_for_characters DynamicLight
    /// </summary>
    public bool is_visible;

    private Camera following_camera;
    private Camera overview_camera;

    private DynamicLight vision;

    private DynamicLight look_for_characters;

    public TrailRenderer trail;

    public Transform attacking_offset;

    public abstract float max_speed { get; set; }
    protected Ability ability_primary;
    protected Ability ability_reload;

    /// <summary>
    /// Skill activated with LShift
    /// </summary>
    protected Ability ability_skill1;

    /// <summary>
    /// Skill activated with Space
    /// </summary>
    protected Ability ability_skill2;


    // Status Ailments to be accessed only from the server.
    private StatusAlteration _stun_status;
    private StatusAlteration _root_status;
    private StatusAlteration _lock_passive;
    private StatusAlteration _lock_primary;
    private StatusAlteration _lock_reload;
    private StatusAlteration _lock_skill1;
    private StatusAlteration _lock_skill2;

    [SyncVar]
    protected bool SA_stunned = false;
    [SyncVar]
    protected bool SA_rooted = false;
    [SyncVar]
    protected bool SA_lock_passive = false;
    [SyncVar]
    protected bool SA_lock_primary = false;
    [SyncVar]
    protected bool SA_lock_reload = false;
    [SyncVar]
    protected bool SA_lock_skill1 = false;
    [SyncVar]
    protected bool SA_lock_skill2 = false;


    // Name Display
    private GUIStyle display_style_mine;
    private GUIStyle display_style_ally;
    private GUIStyle display_style_enemy;
    private GUIStyle display_style_mine_health;
    private GUIStyle display_style_ally_health;
    private GUIStyle display_style_enemy_health;


    // ------------------------------------------------- Unity Overrides -------------------------------------------------

    public override void Start()
    {
        base.Start();
        GUISkin gskn = Resources.Load<GUISkin>("Characters/Display");
        display_style_mine = gskn.FindStyle("DisplayMine");
        display_style_ally = gskn.FindStyle("DisplayAlly");
        display_style_enemy = gskn.FindStyle("DisplayEnemy");
        display_style_mine_health = gskn.FindStyle("DisplayMineHealth");
        display_style_ally_health = gskn.FindStyle("DisplayAllyHealth");
        display_style_enemy_health = gskn.FindStyle("DisplayEnemyHealth");
        StartCoroutine(LookForPlayer());
        StartCoroutine(LookForDL());
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _stun_status = new StatusAlteration(Time.time);
        _root_status = new StatusAlteration(Time.time);
        _lock_passive = new StatusAlteration(Time.time);
        _lock_primary = new StatusAlteration(Time.time);
        _lock_reload = new StatusAlteration(Time.time);
        _lock_skill1 = new StatusAlteration(Time.time);
        _lock_skill2 = new StatusAlteration(Time.time);
        Revive();
    }


    public override void OnStartClient()
    {
        base.OnStartClient();
        ability_primary = new Ability(true, false);
        ability_reload = new Ability(KeyCode.R, true);
        ability_skill1 = new Ability(KeyCode.LeftShift, false);
        ability_skill2 = new Ability(KeyCode.Space, false);
        trail.sortingLayerName = "Player";
        trail.sortingOrder = -1;
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        
        AdjustLayer();
        LocalCamera();
        LocalVision();
        LocalLight();
        OverviewCamera();
    }

    public override void Update()
    {
        base.Update();
        if (isServer)
        {
            ManageStatusAilments();
            Passive();
        }
        if (hasAuthority)
        {
            OverviewCameraSwitch();
            if (IsDead())
                return;
            FaceMouse();
            CheckMovementInputs();
            CheckSkillInputs();
        }
    }

    private void ManageStatusAilments()
    {
        SA_stunned = _stun_status.IsActive();
        SA_rooted = _root_status.IsActive();
        SA_lock_passive = _lock_passive.IsActive();
        SA_lock_primary = _lock_primary.IsActive();
        SA_lock_reload = _lock_reload.IsActive();
        SA_lock_skill1 = _lock_skill1.IsActive();
        SA_lock_skill2 = _lock_skill2.IsActive();
    }

    // -- Status Ailments Inflict
    [Command]
    public void CmdInflictStun(float time)
    {
        _stun_status.Inflict(time);
    }

    [Command]
    public void CmdInflictRoot(float time)
    {
        _root_status.Inflict(time);
    }

    [Command]
    public void CmdInflictLockPassive(float time)
    {
        _lock_passive.Inflict(time);
    }

    [Command]
    public void CmdInflictLockPrimary(float time)
    {
        _lock_primary.Inflict(time);
    }

    [Command]
    public void CmdInflictLockReload(float time)
    {
        _lock_reload.Inflict(time);
    }

    [Command]
    public void CmdInflictLockSkill1(float time)
    {
        _lock_skill1.Inflict(time);
    }

    [Command]
    public void CmdInflictLockSkill2(float time)
    {
        _lock_skill2.Inflict(time);
    }


    // ------------------------------------------------- Setup -------------------------------------------------

    private void AdjustLayer()
    {
        this.gameObject.layer = 5;
        this.transform.FindChild("SpriteDisplay").gameObject.layer = 5;
    }
    
    private void LocalCamera()
    {
        if (FindObjectOfType<LerpFollow>() == null)
            this.following_camera = Instantiate(Resources.Load<GameObject>("Camera/Camera (View Under)")).GetComponent<Camera>();
        else
            this.following_camera = FindObjectOfType<LerpFollow>().GetComponent<Camera>();
        Camera.main.GetComponent<LerpFollow>().target = this.transform;

    }

    private void LocalVision()
    {
        GameObject g = Instantiate(Resources.Load<GameObject>("Characters/Vision"));
        vision = g.GetComponent<DynamicLight>();
        g.transform.position = new Vector3(this.transform.position.x, this.transform.position.y, g.transform.position.z);
        g.transform.parent = this.transform;
        RefreshVision();
    }

    private void LocalLight()
    {
        GameObject g = Instantiate(Resources.Load<GameObject>("Characters/LocalLight"));
        g.transform.position = new Vector3(this.transform.position.x, this.transform.position.y, g.transform.position.z);
        g.transform.parent = this.transform;
    }

    private void OverviewCamera()
    {
        if (FindObjectOfType<OverviewCamera>() == null)
            this.overview_camera = Instantiate(Resources.Load<GameObject>("Camera/Overview Camera")).GetComponent<Camera>();
        else
            this.overview_camera = FindObjectOfType<OverviewCamera>().GetComponent<Camera>();
    }

    public void RefreshVision()
    {
        vision.Rebuild();
        vision.transform.GetChild(0).GetComponent<DynamicLight>().Rebuild();
    }

    // ------------------------------------------------- Check Inputs -------------------------------------------------
    private void OverviewCameraSwitch()
    {
        if (Input.GetKey(KeyCode.Tab))
        {
            overview_camera.enabled = true;
            overview_camera.tag = "MainCamera";
            following_camera.enabled = false;
            following_camera.tag = "Untagged";
            for (int i = 0; i < following_camera.transform.childCount; i++)
                following_camera.transform.GetChild(i).GetComponent<Camera>().enabled = false;
        }
        else
        {
            overview_camera.enabled = false;
            overview_camera.tag = "Untagged";
            following_camera.enabled = true;
            following_camera.tag = "MainCamera";
            for (int i = 0; i < following_camera.transform.childCount; i++)
                following_camera.transform.GetChild(i).GetComponent<Camera>().enabled = true;
        }
    }


    private void CheckMovementInputs()
    {
        if (SA_stunned || SA_rooted)
            return;
        if (Input.GetKey(KeyCode.W))
            Move(Vector2.up);
        if (Input.GetKey(KeyCode.S))
            Move(Vector2.down);
        if (Input.GetKey(KeyCode.A))
            Move(Vector2.left);
        if (Input.GetKey(KeyCode.D))
            Move(Vector2.right);
    }

    private void CheckSkillInputs()
    {
        if (SA_stunned)
            return;
        if (!SA_lock_primary && ability_primary.Pressed() && ability_primary.Ready())
        {
            ability_primary.Use();
            PrimaryAttack();
        }
        if (!SA_lock_reload && ability_reload.Pressed())
        {
            Reload();
        }
        if (!SA_lock_skill1 && ability_skill1.Pressed() && ability_skill1.Ready())
        {
            ability_skill1.Use();
            Skill1();
        }
        if (!SA_lock_skill2 && ability_skill2.Pressed() && ability_skill2.Ready())
        {
            ability_skill2.Use();
            Skill2();
        }
    }

    public virtual void Move(Vector2 dir)
    {
        Vector2 v = GetComponent<Rigidbody2D>().velocity;

        v += dir * max_speed / 2;

        v = Vector2.ClampMagnitude(v, max_speed);
        GetComponent<Rigidbody2D>().velocity = v;
    }

    public abstract void Passive();
    public abstract void PrimaryAttack();
    public abstract void Reload();
    public abstract void Skill1();
    public abstract void Skill2();

    // ------------------------------------------------- Helper Functions -------------------------------------------------

    protected void ShakeCamera(float intensity, float duration, Quaternion dir)
    {
        Camera.main.GetComponent<LerpFollow>().Shake(intensity, duration, dir);
    }


    protected float GetMouseDirection(Vector2 from_position)
    {
        if (Camera.main == null)
            return 0;
        Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float AngleRad = Mathf.Atan2(mouse.y - from_position.y, mouse.x - from_position.x);
        float AngleDeg = (180 / Mathf.PI) * AngleRad;
        return AngleDeg - 90;
    }

    protected void FaceMouse()
    {
        if (SA_stunned || SA_rooted)
            return;
        Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (Vector2.Distance(mouse, transform.position) < Vector2.Distance(attacking_offset.position, transform.position))
            transform.rotation = Quaternion.Euler(0, 0, GetMouseDirection(transform.position));
        else
            transform.rotation = Quaternion.Euler(0, 0, GetMouseDirection(attacking_offset.position));
    }

    protected void Face(Vector2 pos)
    {
        float AngleRad = Mathf.Atan2(pos.y - transform.position.y, pos.x - transform.position.x);
        float AngleDeg = (180 / Mathf.PI) * AngleRad;
        transform.rotation = Quaternion.Euler(0, 0, AngleDeg - 90);
    }

    /// <summary>
    /// Get the closest character to the mouse, besides yourself.
    /// </summary>
    /// <returns></returns>
    protected Character GetClosestCharacterToMouse()
    {
        Character to_return = null;
        Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        foreach (Character c in FindObjectsOfType<Character>())
            if (c != this && c.is_visible && (to_return == null || Vector2.Distance(mouse, c.transform.position) < Vector2.Distance(mouse, to_return.transform.position)))
                to_return = c;
        return to_return;
    }

    protected Character GetClosestAllyToMouse()
    {
        Character to_return = null;
        Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        foreach (Character c in FindObjectsOfType<Character>())
            if (c != this && c.GetTeam() == this.GetTeam() && c.is_visible && (to_return == null || Vector2.Distance(mouse, c.transform.position) < Vector2.Distance(mouse, to_return.transform.position)))
                to_return = c;
        return to_return;
    }

    protected Character GetClosestEnemyToMouse()
    {
        Character to_return = null;
        Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        foreach (Character c in FindObjectsOfType<Character>())
            if (c != this && c.GetTeam() != this.GetTeam() && c.is_visible && (to_return == null || Vector2.Distance(mouse, c.transform.position) < Vector2.Distance(mouse, to_return.transform.position)))
                to_return = c;
        return to_return;
    }

    public override void Dead()
    {
        
        StartCoroutine(RespawnProcess());
    }

    IEnumerator RespawnProcess()
    {
        GameObject d = (GameObject)Instantiate(Resources.Load<GameObject>("Characters/Dead"), transform.position, Quaternion.Euler(0, 0, 0));
        NetworkServer.Spawn(d);
        Destroy(d, 5);
        yield return new WaitForSeconds(5);
        RpcPortToSpawn(GetTeam());
        Reload();
        Revive();
    }


    // ------------------------------------------------- GUI -------------------------------------------------
    protected virtual void OnGUI()
    {
        if (Camera.main == null || Player.mine == null || Player.mine.character == null || player == null)
            return;
        if (Player.mine.character == this)
            GUIDisplayMine();
        else if (this.GetTeam() == Player.mine.character.GetTeam())
            GUIDisplayAlly();
        else
            if (is_visible)
                GUIDisplayEnemy();
    }

    protected virtual void GUIDisplayMine()
    {
        GUIDisplayOnCharacter(display_style_mine, display_style_mine_health);
    }

    protected virtual void GUIDisplayAlly()
    {
        GUIDisplayOnCharacter(display_style_ally, display_style_ally_health);
    }

    protected virtual void GUIDisplayEnemy()
    {
        GUIDisplayOnCharacter(display_style_enemy, display_style_enemy_health);
    }

    private void GUIDisplayOnCharacter(GUIStyle style, GUIStyle style_health)
    {
        if (player != null)
        {
            Vector2 point = Camera.main.WorldToScreenPoint(this.transform.position);
            GUI.Label(new Rect(point.x - 150, Screen.height - point.y - 55, 300, 100), player.player_name, style);

            string s = "";
            for (int i = 0; i < GetHealthLerp() / max_health * 20; i++)
            {
                s += "|";
            }
            GUI.Label(new Rect(point.x - 37, Screen.height - point.y - 40, 300, 11), s, style_health);
        }
    }


    // ------------------------------------------------- Manage Visibility -------------------------------------------------

    private IEnumerator LookForDL()
    {
        while (true)
        {
            if (look_for_characters == null)
            {
                if (GameObject.Find("LookForCharacters") != null)
                {
                    look_for_characters = GameObject.Find("LookForCharacters").GetComponent<DynamicLight>();
                    look_for_characters.InsideFieldOfViewEvent += LookForThis;
                }
            }
            yield return new WaitForSeconds(1);
        }
    }

    private void LookForThis(GameObject[] g)
    {
        is_visible = false;
        foreach (GameObject gs in g)
            if (gameObject.GetInstanceID() == gs.GetInstanceID())
                is_visible = true;
    }


    // ------------------------------------------------- Network Sending -------------------------------------------------

    /// <summary>
    /// Spawn an object on the server. Make sure you only call this on the server,
    /// or else the GameObject won't be passed!
    /// </summary>
    /// <param name="g"></param>
    /// <param name="t"></param>
    [Command]
    protected void CmdSpawn(GameObject g, Team t)
    {
        if (g.GetComponent<NetworkTeam>() == null)
        {
            return;
        }
        g.GetComponent<NetworkTeam>().PreSpawnChangeTeam(t);
        NetworkServer.Spawn(g);

        // Known Errors: HandleTransform no gameObject when object is destroyed by server
        // Only happens when command is called from the client
        // https://issuetracker.unity3d.com/issues/handletransform-no-gameobject-log-error-message-after-destroying-a-networked-object
        // Uncomment this once we know the bug is fixed:
        //g.GetComponent<NetworkIdentity>().AssignClientAuthority(connectionToClient);
    }


    [ClientRpc]
    public void RpcPortToSpawn(Team t)
    {
        if (!hasAuthority)
            return;

        BaseModule base_module = FindObjectOfType<BaseModule>();
        if (t == Team.A)
            transform.position = base_module.SpawnA + new Vector2(MapGenerator.DRAW_PAD + 0.5f, MapGenerator.DRAW_PAD + 0.5f);
        if (t == Team.B)
            transform.position = base_module.SpawnB + new Vector2(MapGenerator.DRAW_PAD + 0.5f, MapGenerator.DRAW_PAD + 0.5f);
    }

    [ClientRpc]
    public void RpcPortToPosition(Vector2 position)
    {
        if (!hasAuthority)
            return;
        transform.position = new Vector3(position.x, position.y, this.transform.position.z);
    }
    
    private void OnUpdatePlayerId(NetworkInstanceId id)
    {
        player_id = id;
        if (ClientScene.FindLocalObject(player_id) == null)
            player = null;
        else
            player = ClientScene.FindLocalObject(player_id).GetComponent<Player>();
    }


    private IEnumerator LookForPlayer()
    {
        while (player == null)
        {
            OnUpdatePlayerId(player_id);
            yield return new WaitForEndOfFrame();
        }
    }



    public virtual void OnDestroy()
    {
        StopAllCoroutines();
        look_for_characters.InsideFieldOfViewEvent -= LookForThis;
    }
}