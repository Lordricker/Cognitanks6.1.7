1️⃣ COLD OPEN
(Gameplay footage rolling. Voice over.)

There's a Flash game from the early 2000s called Bot Arena 3. It's not a complicated game. You assemble a team of AI-controlled bots, give them turrets and armor, and send them into a league-style bracket to fight. The twist is you don't control them — they fight on their own, and your job is to build smarter than your opponent. There's a weight limit each round, so you're constantly making tradeoffs. Do you go heavy armor? Melee turrets? Range turret? There's no right answer.

For some reason, I never forgot it.

(Cut to: workshop bench. Sitting on stool, fiddling with something on the bench — a pen, a small part, whatever's around. This object comes back at the end.)

I'm not a professional game developer. During the day I model real world furniture in Fusion 360, sadly this is not the same as game assets. But for years I had this idea floating around — what if someone made Bot Arena 3 in 3D, and let you actually program the AI yourself? Gladiabots does this pretty well by the way. I even tried to build it back in 2019, got discouraged, and shelved it. The straw that broke the camel's back at that time was getting the money text to flash red if you didn't have the cash for the purchase.

(Reach over and turn on a desk lamp / lightbulb.)

Then AI coding tools hit the market and reignited that spark to make the game. So I started over.

(Cut to: gameplay montage — tank building, node editor, combat.)

This is Cognitanks. You build tanks within a weight limit, you program their behavior using a node-based AI editor — connecting conditions to actions like a flowchart — and you fight through a campaign league. The endgame is asynchronous multiplayer where you post a match, other players join with their own tank teams and AI programs, and it runs without you. Or at least that's how it was supposed to go in my head.

(Cut back to bench. Set something down. Look at camera.)

It got 28 downloads.

(Beat.)

(show text "This is the postmortem.")

---

2️⃣ THE BUILD

My workflow was simple: regular job, family time, kids to bed, then i work on the for an hour or two, in this position (show stick man pic of myself in bed with laptop). And usually I split my focus to watch a show with my wife. Yes, I'm a nerd. Yes I have a wife. Don't question it.

(lean into the mic for dramatic effect)

Some nights I made real progress, other nights I just fought one bug the entire time. There were weeks where I didn't touch it at all. But when I look at the commit timestamps, the core systems were built in about six active months spread across almost a year. The scope really didn't creep on me either — for the most part the original plan was followed.

And I'd say I was having fun a majority of the time. 9 out of 10 i would do it again.

Now side note — I used AI tools, and yes, I also used generative AI for some of the art. If that upsets you then you are certainly entitled to your opinions about it.

(picture of water dumping into chat gpt)

But please don't assume that AI did all the work. AI slop is created by humans using AI as a tool. I'm pretty convinced that AI is not the issue with the game dev scene — the issue is that the gates have opened much wider allowing just about anyone to make games, and a lot of those people just aren't good at making games. And yes, I'm aware I could very well be a part of that sloppy mess.

I would love for a real artist or programmer to invest their time in my game, but that is simply not a reasonable thing to expect for every game being pushed out. why dont we save your talents for games that are actually good.

---

3️⃣ Pain
(Shift posture. Lean forward a bit. More focused energy.)

Alot of the hard problem I hit followed roughly the same cycle. Overengineer it, fail, simplify, argue with the Ai chat about what its supposed to be doing, and then it finally gets solved. I thought it was comical that the ai asked me if it could open unity so it could test the game for me... uh no. Let me walk through the big ones.

(footage of lines not connecting)

**Node Editor Connection Lines.** One of the first big hurdles i had was just drawing connection lines between nodes like on a 2d canvas. Getting them to render correctly, update when dragging, and not break visually took about a week of evening sessions. It sounds small, but my first real road block and man was it annoying. I literally trashed the scripts 5 or 6 times. I think the main issue here was actually using ChatGPT — ChatGPT is not good at coding it turns out. Claude is much better.

**Converting Nodes Into Executable AI.** The whole time I was working on the visual part, my mind was like — how in the world do I connect this to an actual script that the tank AI can read?


I had to make sure it would traverse the graph, determine execution order, prevent infinite loops, and handle modular SubAI references. This wasn't a problem I solved in a night. It stretched across multiple sessions over several weeks, and I kept coming back to it as I added new node types. That system probably pushed my understanding further than anything else in the project. Sometimes you have to mull it over like a cow chewing the cud.


ScriptableObjects in Builds is the next issue and is not related to overengineering — it was just not knowing. I had a lot of the base functionality built and running in the Unity editor, and then I went to play it in an actual build. As it turns out, ScriptableObjects — the Unity data containers I was using to store all the AI behavior files, —per the recommendation of the ai chat — can't be created or modified at runtime in a build. They only work that way inside the editor.

I found this out after the whole saving and loading system was already built around them. I wrote in my dev log that day: "needless to say this is a disheartening setback. why not just get good."

(Point at camera.)

I blame you, AI chat. You could have told me.



I Spent about a week converting everything to JSON storage — basically plain text that can be read and written at runtime. Solved it completely, but it was a pretty boring detour. can you tell that im a noob?

**Tank Physics.** I started with Unity's NavMesh system — basically a built-in pathfinding tool — but it didn't feel like tanks. The movement was too clean, too instant, and how dare they not follow the terrain. So I scrapped it and switched to physics-based movement where forces are actually applied to the tank body.

(Slide something across the bench surface to demonstrate the movement idea.)

That decision cost a lot of time, but I felt it was important since it's kind of the main part of the game. I fought gravity, friction, and rotation — still not following terrain real well as it turns out — without getting anywhere satisfying. This was July.

At this point the initial rush wore off, and as I looked at the project my motivations really dipped. so I took a break. More on that in a second.
(show 5 months later meme picture, "literally")

I Came back in December, and after about three sessions — maybe four or five hours total — I had a breakthrough on the physics: separate the forward driving force from the turning force so they can be tuned independently.

Tuning those independently produced an acceptable result.

As a funny footnote — I later suspected I may have scaled the tanks incorrectly all along, which might explain a lot of the physics weirdness I was fighting. But at that point I was pretty much done and it isn't worth fixing for multiple reasons.

**Artillery.**

(Toss something lightly in an arc across the bench.)

I wanted an artillery turret that fires in an arc with splash damage. The physics of doing that in Unity are not complicated in theory, but we all know theory is not one-to-one with reality. Not even game reality.

After about two days I gave up on Unity's physics and tried custom ballistics simulation from scratch. That also didn't work. So I went back to Unity physics, barely changed anything, and it just... worked. Two days total, and I still don't fully know what changed.

Right after that did the shotgun (dramatic pause), aaand that took about ten minutes, ok moving on.

**Multiplayer.** Implementing asynchronous multiplayer was a thing. Discord login kept giving me the runaround. Match posting, other players joining with their own tank loadouts and AI files, the match running and saving a replay — I set that up on a Firebase server and it went rather smoothly.

From the first commit where I started setting it up to the commit message "multiplayer match actually ran" was about one week. Genuinely one of the more satisfying moments of the whole project.

(Pause. More measured tone.)

I should point out that while multipleayer does work it is not deterministic. Meaning you can watch the same match twice and it can have different results. Another one of those things that im just not going to worry about since... well im kinda the only person that would be worrying about it.

---

4️⃣ THE WALL
(Sit back. Quieter energy. Maybe set hands flat on the bench.)

I want to touch on the motivation issue, since I'm fairly certain a lot of devs run into this.

The break between July and December. It wasn't because the project was impossible. It was because I needed to figure out why I was doing this. Apparently "to show it off and have people be interested" was not a strong enough reason.

(Look away from camera briefly, then back.)

I posted to Reddit, itch forums, messaged streamers, asked friends to try it.

The response wasn't explicitly negative. It was mainly just... crickets.

(Silence for a beat. Let it breathe.)

Stepping away helped. When I came back and finished multiplayer and added more arenas, it felt like closure instead of pressure. I had to look at it more as a self-improvement project. That shift in framing is what got me across the finish line.

---

5️⃣ THE NUMBERS
(Straighten up. Direct to camera.)

In the end — 115 views, 28 downloads, about a 5% click-through rate.

For a download-only itch game, that conversion rate is actually pretty great.

The issue wasn’t that people who saw it didn’t want it — it’s that not many people saw it.

Distribution is apparently completely different skill set than development.

---

6️⃣ RESOLUTION
(Calmer. Final beat energy. Maybe glance down at the bench, then back up.)

I think I’ve reached a natural stopping point.

Not because the project failed. Not because it was too hard.

But because that kid who played Bot Arena 3 and thought "I wish I could make something like that" — finally made that dream come true.

If traction ever happens, I'd absolutely expand it. But even if it doesn't — It was definitely a good learning experience.


And for me, that makes it worth it.

(Hold for a beat. Cut.)