using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class CombatUnit : MonoBehaviour
{
    public bool isPrototype = false;
    public int teamId = 0; // 0 or 1
    public int maxAmount = 10;
    public float moveCooldown = 1f;
    public float moveRange = 1f;
    public float attackCooldown = 1f;
    public float attackRange = 2f;
    public int attackDamage = 10;
    public int health = 100;
    public int childrenCount = 0;

    //public string sfxSpawn;
    //public string sfxMove;
    //public string sfxAttack;
    //public string sfxHit;
    //public string sfxDeath;

    static List<CombatUnit> team0 = new List<CombatUnit>();
    static List<CombatUnit> team1 = new List<CombatUnit>();

    SpriteRenderer sprite;
    Transform visual;
    Collider2D col;
    Color defaultColor;
    Vector3 defaultScale;
    Tween attackColorTween; // For white flash
    Tween damageColorTween; // For red flash
    Tween damageScaleTween; // For damage scale bounce
    GameObject teamContainer;
    static float globalNextSpawnTime = 0f;
    static float globalSpawnCooldown = .5f; // Adjust as needed
    static bool globalSpawnInProgress = false;

    int currentHealth;
    bool isDead = false;
    bool canAct = true;

    int spawnedCount = 0;
    List<CombatUnit> myTeamList => teamId == 0 ? team0 : team1;
    List<CombatUnit> enemyTeamList => teamId == 0 ? team1 : team0;


    void Start()
    {
        sprite = GetComponentInChildren<SpriteRenderer>();
        col = GetComponentInChildren<Collider2D>();
        visual = sprite.transform;
        defaultColor = sprite.color;
        defaultScale = visual.localScale;

        if (isPrototype)
        {
            sprite.enabled = false;
            if (col) col.enabled = false;
            StartCoroutine(SpawnerLoop());
        }
        else
        {
            sprite.enabled = true;
            if (col) col.enabled = true;
            myTeamList.Add(this);
            currentHealth = health;
            //PlaySFX(sfxSpawn);
            StartCoroutine(BehaviorLoop());
        }
    }

    IEnumerator SpawnerLoop()
    {
        Debug.Log("Spawner Loop running at " + Time.realtimeSinceStartup);
        teamContainer = new GameObject($"Team{teamId}Container");
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(-globalSpawnCooldown, globalSpawnCooldown));
            CleanupDead();

            // Wait until global cooldown is over and no other spawn is in progress
            while ((spawnedCount < maxAmount) &&
                   (Time.time < globalNextSpawnTime || globalSpawnInProgress))
            {
                yield return null;
            }

            if (spawnedCount < maxAmount && Time.time >= globalNextSpawnTime && !globalSpawnInProgress)
            {
                globalSpawnInProgress = true;

                Vector2 pos = new Vector2(Random.Range(-8f, 8f), Random.Range(-4f, 4f));
                GameObject clone = Instantiate(gameObject, pos, Quaternion.identity);

                CombatUnit unit = clone.GetComponent<CombatUnit>();
                unit.transform.SetParent(teamContainer.transform);
                unit.isPrototype = false;
                childrenCount++;
                unit.name = name + childrenCount;
                spawnedCount++;

                globalNextSpawnTime = Time.time + globalSpawnCooldown;

                // Wait one frame before allowing another spawn
                yield return null;
                globalSpawnInProgress = false;
            }
        }
    }



    void CleanupDead()
    {
        myTeamList.RemoveAll(u => u == null);
        enemyTeamList.RemoveAll(u => u == null);
        spawnedCount = myTeamList.Count;
    }

    IEnumerator BehaviorLoop()
    {
        while (!isDead)
        {
            CombatUnit target = FindClosestEnemy();
            if (target != null)
            {
                float dist = Vector2.Distance(transform.position, target.transform.position);
                if (dist > attackRange)
                {
                    yield return MoveStep(target.transform.position);
                }
                else
                {
                    yield return Attack(target);
                }
            }
            yield return null;
        }
    }

    IEnumerator MoveStep(Vector3 targetPos)
    {
        //PlaySFX(sfxMove);
        ResetColor();

        Vector3 dir = (targetPos - transform.position).normalized;
        Vector3 goal = transform.position + dir * moveRange;

        // Add ±10% randomness to movement duration
        float duration = moveCooldown * 0.5f * Random.Range(0.9f, 1.1f);
        float timer = 0f;
        Vector3 start = transform.position;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            transform.position = Vector3.Lerp(start, goal, t);
            float bob = Mathf.Sin(t * Mathf.PI) * 0.2f;
            visual.localPosition = new Vector3(0, bob, 0);
            yield return null;
        }

        visual.localPosition = Vector3.zero;
        ResetColor();
        yield return new WaitForSeconds(moveCooldown * 0.5f);
    }

    IEnumerator Attack(CombatUnit target)
    {
        //PlaySFX(sfxAttack);

        // Calculate lunge direction (local space)
        Vector3 dir = (target.transform.position - transform.position).normalized;
        float lungeDistance = 0.2f; // Adjust for desired snap distance
        Vector3 lungeTarget = dir * lungeDistance;

        // Kill any previous lunge tween
        visual.DOKill();

        // Lunge tween: snap out and back
        visual.DOLocalMove(lungeTarget, 0.07f)
            .SetLoops(2, LoopType.Yoyo)
            .SetEase(Ease.OutQuad);

        // Rotation and color flash
        visual.DORotate(new Vector3(0, 0, 45f), 0.1f)
            .SetLoops(2, LoopType.Yoyo);

        if (attackColorTween != null && attackColorTween.IsActive())
            attackColorTween.Kill();

        if (damageColorTween == null || !damageColorTween.IsActive())
        {
            attackColorTween = sprite.DOColor(Color.white, 0.05f).SetLoops(2, LoopType.Yoyo)
                .OnComplete(() =>
                {
                    if (damageColorTween == null || !damageColorTween.IsActive())
                        ResetColor();
                });
        }

        float cooldown = attackCooldown * Random.Range(0.85f, 1.15f);
        yield return new WaitForSeconds(cooldown);

        int dmg = Mathf.RoundToInt(attackDamage * Random.Range(0.9f, 1.1f));
        if (target != null && !target.isDead) target.TakeDamage(dmg);

        // Ensure visual returns to original position
        visual.localPosition = Vector3.zero;

        if (damageColorTween == null || !damageColorTween.IsActive())
            ResetColor();
    }

    public void TakeDamage(int dmg)
    {
        //PlaySFX(sfxHit);
        if (isDead) return;
        currentHealth -= dmg;

        // Kill only the previous scale tween, not all tweens on visual
        if (damageScaleTween != null && damageScaleTween.IsActive())
            damageScaleTween.Kill();

        // Bounce scale elastically (hit effect)
        float bounceDuration = 0.35f;
        float bounceScale = 1.3f;
        visual.localScale = defaultScale; // Always reset before animating

        damageScaleTween = visual.DOScale(defaultScale * bounceScale, bounceDuration * 0.5f)
         .SetEase(Ease.OutElastic)
         .OnComplete(() =>
         {
             damageScaleTween = visual.DOScale(defaultScale, bounceDuration * 0.5f)
                 .SetEase(Ease.OutElastic)
                 .OnComplete(() => visual.localScale = defaultScale);
         });

        // Always override with red flash
        if (damageColorTween != null && damageColorTween.IsActive())
            damageColorTween.Kill();

        damageColorTween = sprite.DOColor(Color.red, 0.1f).SetLoops(2, LoopType.Yoyo)
            .OnComplete(() =>
            {
                if (attackColorTween != null && attackColorTween.IsActive())
                    sprite.color = Color.white;
                else
                    ResetColor();
            });

        if (currentHealth <= 0)
        {
            Die();
        }
        // No need to call ResetColor here; handled in OnComplete above
    }

    void ResetColor()
    {
        if (attackColorTween != null && attackColorTween.IsActive())
            attackColorTween.Kill();
        if (damageColorTween != null && damageColorTween.IsActive())
            damageColorTween.Kill();
        if (damageScaleTween != null && damageScaleTween.IsActive())
            damageScaleTween.Kill();
        sprite.color = defaultColor;
        visual.localScale = defaultScale;
    }
    //private void //PlaySFX(string sfx)
    //{
    //    if (!string.IsNullOrEmpty(sfx))
    //        ZoundEngine.PlayZound(sfx);
    //}

    void Die()
    {
        isDead = true;
        ResetColor(); // Ensure color is normal before fading out
        sprite.DOFade(0f, 1f).OnComplete(() => Destroy(gameObject));
        myTeamList.Remove(this);
    }

    CombatUnit FindClosestEnemy()
    {
        CombatUnit closest = null;
        float closestDist = Mathf.Infinity;
        foreach (CombatUnit enemy in enemyTeamList)
        {
            if (enemy == null || enemy.isDead) continue;
            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < closestDist)
            {
                closest = enemy;
                closestDist = dist;
            }
        }
        return closest;
    }

    void OnDestroy()
    {
        myTeamList.Remove(this);
        enemyTeamList.Remove(this);
    }
}
