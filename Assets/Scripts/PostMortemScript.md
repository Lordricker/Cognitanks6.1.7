1️⃣ Cold Open (Gameplay Playing)

There's a Flash game from the early 2000s called Bot Arena 3. It's not a complicated game. You assemble a team of AI-controlled bots, give them turrets and armor, and send them into a league-style bracket to fight. The twist is you don't control them — they fight on their own, and your job is to build smarter than your opponent. There's a weight limit each round, so you're constantly making tradeoffs. Do you go heavy armor? melee turrets? range turret? There's no right answer.

For some reason, I never forgot it.

I'm not a professional game developer. During the day I real world furniture in Fusion 360, sadly not the same as game assets. But for years I had this idea floating around — what if someone made Bot Arena 3 in 3D, and let you actually program the AI yourself, gladiabots does this pretty well by the way... I even tried to build it back in 2019, got discouraged, and shelved it. The straw that broke the camels back at that time was getting the money text to flash red if you didnt have the cash for the purchase

Then (turn on lightbulb) AI coding tools got good enough that suddenly building something like that felt possible for someone like me. So I started over. 

This is Cognitanks. You build tanks within a weight limit, you program their behavior using a node-based AI editor — connecting conditions to actions like a flowchart — and you fight through a campaign league. The endgame is asynchronous multiplayer where you post a match, other players join with their own tank builds and AI programs, and it runs without you. or at least thats how it was supposed to go in my head

I worked on it about one to two hours a night, usually after the kids were in bed. In total it was roughly six active months of development spread across almost a year, with some real gaps in between.

It ended up with physics-based tank movement, a fully functional node editor, modular AI systems, multiple arenas, functional shop, cosmetics, and asynchronous multiplayer.

It got 28 downloads.

This is the postmortem.

2️⃣ The Plan From The Start

The scope really didnt creep on me. Sure things took longer than expected but for the most part I really feel that the original plan was mostly followed.

And honestly, it moved faster than I expected.

3️⃣ How It Actually Got Built

My workflow was simple: work, family time, kids to bed, then Unity for an hour or two. And usually I split my focus to watch a show with my wife. yes, im a nerd, yes I have a wife, dont question it!

Some nights I made real progress, other nights I just fought one bug the entire time.

There were weeks where I didn’t touch it at all.

But when I look at the timestamps, the core systems were built in about six active months. And id say I was having fun a majority of the time, 9/10 would do again.

4️⃣ Hardest Technical Problems
ok lets talk about some specific issues. skip to ---- if you are not all that interested in the technical side.

Node Editor Connection Lines

One of the first big hurdles was just drawing connection lines between nodes.

Getting them to render correctly, update when dragging nodes, and not break visually took about a week of evening sessions. It sounds small, but it was my first real "this is harder than I thought" moment. I literally trashed the scripts like 5 or 6 times. I think the main issue here was actually using chatgpt, chatgpt is not good at coding it turns out, claude is much better.

Converting Visual Nodes Into Executable AI

The whole time I was working on the visual part my mind was like how in the world do i connect this to an actual script that the tank AI can read...

I had to figure out how to traverse the graph, determine execution order, prevent infinite loops, and handle modular SubAI references. If it wasnt right this would be a glaring issue.

This wasn't a problem I solved in a night. It stretched across multiple sessions over several weeks, and I kept coming back to it as I added new node types. That system probably pushed my understanding further than anything else in the project. Some times you have to mull it over like a cow chewing the cudd.

ScriptableObjects in Builds

At one point, I had a lot of the base functionality built and running in the Unity editor but then I went play it in an actual build. As it turns out ScriptableObjects — the Unity data containers I was using to store all the AI behavior files — can't be created or modified at runtime in a build. They only work that way inside the editor.

I found this out after the whole AI saving and loading system was already built around them. I wrote in my dev log that day: "needless to say this is a disheartening setback." I blame you ai chat, you could have told me.

I spent about a week converting everything to JSON serialization — which is basically plain text format that can be read and written at runtime. It solved the problem completely, but it was a pretty boring detour that I didn't see coming.

Tank Physics

I started with Unity's NavMesh system — basically a built-in pathfinding tool — but it didn't feel like tanks. The movement was too clean, too instant, also how dare they not follow the terrain. So I scrapped it and switched to physics-based movement where forces are actually applied to the tank body.

That decision cost a lot of time but I felt it was important since its kinda the main part of the game. I fought gravity, friction and rotation (yes still not following terrain turns out) without getting anywhere satisfying. This was July. At this point the initial rush wore off and as looked at the project I kind took a motivation hit and I took a break, more on that later. Came back in December, and after about three sessions — maybe four or five hours total — I figured out the key thing: separate the forward driving force from the turning force so they can be tuned independently. Tuning these produced an acceptable result.

As a funny footnote, I later suspected I may have scaled the tanks incorrectly all along, which might explain a lot of the physics weirdness I was fighting. But at that point I was pretty much done with the project and it isnt worth fixing  for multiple reasons.

Artillery

I wanted an artillery turret that fires in an arc with splash damage. The physics of doing that in Unity are not complicated in theory, but we all no theory is not 1 to 1 with reality, not even game reality

After about two days of that I gave up on Unity's physics and started writing my own ballistics simulation from scratch. That also didn't work. So I went back to Unity physics, barely changed anything, and it just... worked. Two days total, and I still don't fully know what changed.

Shotgun, for comparison, took about ten minutes.

Multiplayer and Nondeterminism

Implementing asynchronous multiplayer was a thing. Discord login kept giving me the run around. Match posting, other players joining with their own tank loadouts and AI files, the match running and saving a replay I set up on a firebase server and it went rather smoothly.

From the first commit where I started setting it up to the commit message "multiplayer match actually ran" was about one week. Genuinely one of the more satisfying moments of the whole project.

I had a suspicion that the replays weren't deterministic — meaning the match that was saved might not have played out the same way as the live match. The same inputs, slightly different outcomes. That's a known hard problem in game development and I didn't have a clean solution for it. If there was any interest in the game then id revisit this.

5️⃣ Motivation Dip

I wanted to touch on the motivation issue, since im fairly certain a lot of devs run into this.

The break between july and december. It wasnt because the project was impossible, it was because I needed to figure out why I was doing this, apparently to show it off and people be interested in it was not a strong reason.

I posted to Reddit, itch forums, messaged streamers, and asked friends to try it.

The response wasn’t explicitly negative, it was mainly just crickets.

Stepping away actually helped, and when I came back and finished multiplayer and added more arenas, it felt like closure instead of pressure. I had to look at it more as a self improvement project.

6️⃣ AI Coding Reality
"but you used ai and thats ill legitimate" 

Yes, I used ai. Yes I used generative ai for some of the art. If that upsets you then you are certainly entitled to your opinion.

However please do not assume ai did all the work. AI slop is created by humans using ai as a tool. I am pretty convinced that Ai is not the issue with the game dev scene, the issue is that the gates have opened much wider allowing just about anyone to make games, and a lot of those people just are not good at making games. And yes im aware i could very well be apart of that group. 

Also I would love for a real artist or programmer to invest their time in my game, but that is simply not a reasonable thing to expect for every game being pushed out, lets save your talents for games that are actually good.

7️⃣ The Numbers

In the end, the game got 115 views, 28 downloads, and about a 5% click-through rate.

For a download-only itch game, that conversion rate is actually pretty great.

The issue wasn’t that people who saw it didn’t want it — it’s that not many people saw it.

Distribution is a completely different skill set than development.

8️⃣ What Actually Exists Now

There’s a functioning node-based AI editor.

Modular SubAI systems.

Physics-based tank movement.

A functional Shop.

Asynchronous multiplayer.

Cosmetic customization.

All built as a side hobby.

9️⃣ Resolution

I think I’ve reached a natural stopping point.

Not because the project failed and not because it was too hard.

But because I proved to myself that I could build it.

If traction ever happens, I’d absolutely expand it.

But even if it doesn’t, I learned more building this than I would have just thinking about it.

And for me, that makes it worth it.