# IMPORTANT NOTE
My mods will no longer be available on The Forge.
According to their rules, you won’t receive support if you’ve installed mods that weren’t published there. You’ll be advised to uninstall these (my) mods or be denied support altogether.
If you have any issues with my mods, please contact me directly on Discord.

**Discord Name:** *@obviously.senze*  
**My Discord server:** https://discord.gg/PD5TPQFKpa


# ARMY OF TWO
Introducing two new bosses; `Rook` & `Tombstone`.  
**Rook** is the leader and is more of the aggressive, tanky type, armed with an **HK416** assault rifle, a **M60E6** LMG or an **AK-74M**.  
He has **1,240 health points**.

**Tombstone**, on the other hand, is more of a sniper and is equipped with an **SR-25 marksman rifle**, a **Accuracy International AXMC .338 LM bolt-action sniper rifle** or a **SVDS sniper rifle**.
Tombstone can place tripwires and has **940 health points**.

Both use a Level VI **First Spear Siege-R Optimized M.A.S.S. Plate Carrier (Black Division)** with a modified **Shattered lightweight armored mask**, also Level VI.

**SPT version:** ~4.1.6  
**Dependencies:**  
- MoreBotsAPI (https://sp-mod.com/mod/2426/morebotsapi)
- SAIN (https://sp-mod.com/mod/791/sain-solarints-ai-modifications-full-ai-combat-system-replacement)
- WTT - CommonLib (https://sp-mod.com/mod/2310/wtt-commonlib)
- WTT - Content Backport (https://sp-mod.com/mod/2512/wtt-content-backport)  

**FIKA compatible:** unknown  
**Recommendations:**  
**Incompatibilities:** None that I know of

# DETAILS
Their spawn probability and spawn location can be adjusted after the server starts at http://127.0.0.1:6971/.  
Simply copy the link into your browser of choice.
After making any changes, simply click `Commit Deployment`.  
By default, Rook and Tombstone are found on *Shoreline, Reserve, and Customs* (15% with default settings).

**VENDETTA-HUNT-SYSTEM**  
The Vendetta system keeps track of encounters, injuries, kills, and extracted masks across raids.  
As soon as the configurable heat limit is reached, Rook flags the player and announces the hunt via a message.  
Army of Two is guaranteed to spawn in the next eligible raid and will actively search for the player.  
The Vendetta does not end until both have been killed and the player has been successfully extracted.


# SAIN
Rook and Tombstone can be customized in the SAIN menu (F6 by default).

I'm referring here to a note from *MoreBotsAPI*
> It seems I've found what causes a non-aggression bug, at least with SAIN and my bot mods. 
> If you uninstall a custom bot mod that uses MoreBotsAPI, SAIN will retain references to the now-removed bots in its “Default Bot Config Values” folder, causing client errors that prevent SAIN from loading properly for bots and disabling their combat behaviors. 
> Deleting the files in this folder and letting SAIN reset them fixes the problem.


# BUG REPORTS
It's very important to me that, when there are bugs, I receive server logs, client logs, and, if possible, video footage. 
Please use https://discord.com/channels/1551226098354753576/1552431523658203307 for this.
