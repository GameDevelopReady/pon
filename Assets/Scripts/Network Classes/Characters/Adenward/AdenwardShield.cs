﻿using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

public class AdenwardShield : NetworkEntity
{
    public override float max_health { get { return 500; } set { throw new NotImplementedException(); } }
    public Adenward owner;
    private const int WAIT_TIME_BEFORE_REGEN = 5;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Revive();
    }

    public override void Update()
    {
        base.Update();
        if (isServer)
        {
            ChangeTeam(owner.GetTeam());
            if (owner.stronghold_mode)
                ChangeHealth(Time.deltaTime * 150);
            else if (Time.time - time_of_recent_damage > WAIT_TIME_BEFORE_REGEN)
                ChangeHealth(Time.deltaTime * 50);
        }
        ManageAlpha();
    }

    private void ManageAlpha()
    {
        if (owner.IsDead())
        {
            GetComponent<Collider2D>().enabled = false;
            GetComponent<SpriteRenderer>().color = new Color(GetComponent<SpriteRenderer>().color.r,
                                                                GetComponent<SpriteRenderer>().color.g,
                                                                GetComponent<SpriteRenderer>().color.b,
                                                                0);
            return;
        }
        else
        {
            GetComponent<Collider2D>().enabled = true;
        }

        if (IsDead())
        {
            GetComponent<SpriteRenderer>().color = new Color(GetComponent<SpriteRenderer>().color.r,
                                                                GetComponent<SpriteRenderer>().color.g,
                                                                GetComponent<SpriteRenderer>().color.b,
                                                                0);
            transform.GetChild(0).GetComponent<SpriteRenderer>().color = new Color(transform.GetChild(0).GetComponent<SpriteRenderer>().color.r,
                                                                                    transform.GetChild(0).GetComponent<SpriteRenderer>().color.g,
                                                                                    transform.GetChild(0).GetComponent<SpriteRenderer>().color.b,
                                                                                    0);
            return;
        }
        if (owner.show_shield)
        {
            GetComponent<BoxCollider2D>().enabled = true;
            GetComponent<SpriteRenderer>().color = new Color(GetComponent<SpriteRenderer>().color.r,
                                                                GetComponent<SpriteRenderer>().color.g,
                                                                GetComponent<SpriteRenderer>().color.b,
                                                                Mathf.Lerp(GetComponent<SpriteRenderer>().color.a, 1, Time.deltaTime * 10));
            transform.GetChild(0).GetComponent<SpriteRenderer>().color = new Color(transform.GetChild(0).GetComponent<SpriteRenderer>().color.r,
                                                                                    transform.GetChild(0).GetComponent<SpriteRenderer>().color.g,
                                                                                    transform.GetChild(0).GetComponent<SpriteRenderer>().color.b,
                                                                                    Mathf.Lerp(GetComponent<SpriteRenderer>().color.a, 1, Time.deltaTime * 40));
        }
        else
        {
            GetComponent<BoxCollider2D>().enabled = false;
            GetComponent<SpriteRenderer>().color = new Color(GetComponent<SpriteRenderer>().color.r,
                                                                GetComponent<SpriteRenderer>().color.g,
                                                                GetComponent<SpriteRenderer>().color.b,
                                                                Mathf.Lerp(GetComponent<SpriteRenderer>().color.a, 0, Time.deltaTime * 10));
            transform.GetChild(0).GetComponent<SpriteRenderer>().color = new Color(transform.GetChild(0).GetComponent<SpriteRenderer>().color.r,
                                                                                    transform.GetChild(0).GetComponent<SpriteRenderer>().color.g,
                                                                                    transform.GetChild(0).GetComponent<SpriteRenderer>().color.b,
                                                                                    Mathf.Lerp(GetComponent<SpriteRenderer>().color.a, 0, Time.deltaTime * 40));
        }

        
    }

    public override void Dead()
    {
        StartCoroutine(RespawnProcess());
    }

    IEnumerator RespawnProcess()
    {
        GetComponent<BoxCollider2D>().enabled = false;
        yield return new WaitForSeconds(5);
        GetComponent<BoxCollider2D>().enabled = true;
        Revive();
    }
}