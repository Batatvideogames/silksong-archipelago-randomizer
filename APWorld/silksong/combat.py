from __future__ import annotations

SKILLS_BY_TIER = {
    'low': ('Rune Rage',),
    'mid': ('Cross Stitch', 'Pale Nails', 'Sharpdart'),
    'high': ('Silkspear', 'Thread Storm'),
}
TOOLS_BY_TIER = {
    'low': ('Straight Pin', 'Threefold Pin', 'Longpin', 'Curveclaw', 'Conchcutter',
            'Silkshot (Twelfth Architect)', 'Silkshot (Forge Daughter)', "Delver's Drill"),
    'mid': ('Curvesickle', 'Throwing Ring', 'Pimpillo', 'Silkshot (Original)',
            'Cogwork Wheel', 'Rosary Cannon', 'Flintslate', 'Snare Setter', 'Flea Brew'),
    'high': ('Sting Shard', 'Tacks', 'Cogfly', 'Voltvessels'),
}
TIERS = ('low', 'mid', 'high')


_PROFILES = {
    'boss:bell-beast': {'name': 'Bell Beast', 'needle': 0, 'dps': None, 'movement': ''},
    'boss:fourth-chorus': {'name': 'Fourth Chorus', 'needle': 0, 'dps': None, 'movement': ''},
    'boss:great-conchflies': {'name': 'Great Conchflies', 'needle': 0, 'dps': None, 'movement': ''},
    'boss:lace-deep-docks': {'name': 'Lace (Deep Docks)', 'needle': 0, 'dps': None, 'movement': ''},
    'boss:last-judge': {'name': 'Last Judge', 'needle': 0, 'dps': None, 'movement': '(Faydown OR Dash)'},
    'boss:moorwing': {'name': 'Moorwing', 'needle': 0, 'dps': None, 'movement': ''},
    'boss:moss-mother-ruined-chapel': {'name': 'Moss Mother (Ruined Chapel)', 'needle': 0, 'dps': None, 'movement': ''},
    'boss:moss-mother-weavenest-atla': {'name': 'Moss Mother (Weavenest Atla)', 'needle': 1, 'dps': None, 'movement': ''},
    'boss:phantom': {'name': 'Phantom', 'needle': 0, 'dps': None, 'movement': ''},
    'boss:savage-beastfly-chapel-of-the-beast': {'name': 'Savage Beastfly (Chapel of the Beast)', 'needle': 0, 'dps': None, 'movement': '(Dash OR Faydown OR Clawline)'},
    'boss:sister-splinter': {'name': 'Sister Splinter', 'needle': 0, 'dps': None, 'movement': 'Dash OR Clawline'},
    'boss:skull-tyrant-the-marrow': {'name': 'Skull Tyrant (The Marrow)', 'needle': 0, 'dps': None, 'movement': ''},
    'boss:skull-tyrant-bone-bottom': {'name': 'Skull Tyrant (Bone Bottom)', 'needle': 0, 'dps': None, 'movement': ''},
    'boss:widow': {'name': 'Widow', 'needle': 0, 'dps': None, 'movement': "(Faydown OR Drifter's)"},
    'boss:broodmother': {'name': 'Broodmother', 'needle': 1, 'dps': None, 'movement': ''},
    'boss:cogwork-dancers': {'name': 'Cogwork Dancers', 'needle': 2, 'dps': 'mid', 'movement': "(Drifter's OR Faydown) AND Dash"},
    'boss:disgraced-chef-lugoli': {'name': 'Disgraced Chef Lugoli', 'needle': 1, 'dps': None, 'movement': ''},
    'boss:father-of-the-flame': {'name': 'Father of the Flame', 'needle': 1, 'dps': None, 'movement': 'Faydown'},
    'boss:first-sinner': {'name': 'First Sinner', 'needle': 1, 'dps': None, 'movement': ''},
    'boss:forebrothers-signis-gron': {'name': 'Forebrothers Signis & Gron', 'needle': 3, 'dps': 'mid', 'movement': ''},
    'boss:garmond-and-zaza': {'name': 'Garmond and Zaza', 'needle': 1, 'dps': None, 'movement': 'Dash'},
    'boss:grand-mother-silk': {'name': 'Grand Mother Silk', 'needle': 1, 'dps': None, 'movement': "(Faydown OR Drifter's) AND Dash"},
    'boss:groal-the-great': {'name': 'Groal the Great', 'needle': 1, 'dps': None, 'movement': '(Dash OR Faydown)'},
    'boss:lace-the-cradle': {'name': 'Lace (The Cradle)', 'needle': 1, 'dps': None, 'movement': 'Dash'},
    'boss:raging-conchfly': {'name': 'Raging Conchfly', 'needle': 1, 'dps': None, 'movement': ''},
    'boss:savage-beastfly-far-fields': {'name': 'Savage Beastfly (Far Fields)', 'needle': 1, 'dps': None, 'movement': ''},
    'boss:second-sentinel': {'name': 'Second Sentinel', 'needle': 1, 'dps': None, 'movement': ''},
    'boss:shakra': {'name': 'Shakra', 'needle': 1, 'dps': None, 'movement': ''},
    'boss:summoned-saviour': {'name': 'Summoned Saviour', 'needle': 1, 'dps': None, 'movement': ''},
    'boss:the-unravelled': {'name': 'The Unravelled', 'needle': 1, 'dps': None, 'movement': ''},
    'boss:trobbio': {'name': 'Trobbio', 'needle': 1, 'dps': 'mid', 'movement': ''},
    'boss:voltvyrm': {'name': 'Voltvyrm', 'needle': 1, 'dps': 'mid', 'movement': ''},
    'boss:bell-eater': {'name': 'Bell Eater', 'needle': 3, 'dps': 'high', 'movement': ''},
    'boss:clover-dancers': {'name': 'Clover Dancers', 'needle': 3, 'dps': None, 'movement': ''},
    'boss:crawfather': {'name': 'Crawfather', 'needle': 3, 'dps': 'mid', 'movement': ''},
    'boss:crust-king-khann': {'name': 'Crust King Khann', 'needle': 3, 'dps': None, 'movement': ''},
    'boss:gurr-the-outcast': {'name': 'Gurr the Outcast', 'needle': 3, 'dps': None, 'movement': ''},
    'boss:lost-garmond': {'name': 'Lost Garmond', 'needle': 3, 'dps': None, 'movement': ''},
    'boss:lost-lace': {'name': 'Lost Lace', 'needle': 4, 'dps': 'high', 'movement': ''},
    'boss:nyleth': {'name': 'Nyleth', 'needle': 4, 'dps': 'high', 'movement': ''},
    'boss:palestag': {'name': 'Palestag', 'needle': 3, 'dps': 'high', 'movement': ''},
    'boss:pinstress': {'name': 'Pinstress', 'needle': 3, 'dps': 'mid', 'movement': ''},
    'boss:plasmified-zango': {'name': 'Plasmified Zango', 'needle': 3, 'dps': 'mid', 'movement': ''},
    'boss:shrine-guardian-seth': {'name': 'Shrine Guardian Seth', 'needle': 4, 'dps': 'high', 'movement': ''},
    'boss:skarrsinger-karmelita': {'name': 'Skarrsinger Karmelita', 'needle': 4, 'dps': 'high', 'movement': ''},
    'boss:tormented-trobbio': {'name': 'Tormented Trobbio', 'needle': 3, 'dps': 'high', 'movement': ''},
    'boss:watcher-at-the-edge': {'name': 'Watcher at the Edge', 'needle': 4, 'dps': 'high', 'movement': ''},
    'gauntlet:chapel-of-the-wanderer': {'name': 'Chapel of the Wanderer', 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:deep-docks-center': {'name': 'Deep Docks Center', 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:deep-docks-entrance': {'name': 'Deep Docks Entrance', 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:deep-docks-southeast': {'name': 'Deep Docks Southeast', 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:greymoor-tower': {'name': 'Greymoor Tower', 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:craw-lake': {'name': 'Craw Lake', 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:chapel-of-the-reaper': {'name': 'Chapel of the Reaper', 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:hunter-s-march-entrance': {'name': "Hunter's March Entrance", 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:hunter-s-march-center': {'name': "Hunter's March Center", 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:hunter-s-march-east': {'name': "Hunter's March East", 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:hunter-s-march-northeast': {'name': "Hunter's March Northeast", 'needle': 1, 'dps': 'mid', 'movement': ''},
    'gauntlet:hunter-s-march-northeast-2': {'name': "Hunter's March Northeast #2", 'needle': 1, 'dps': 'mid', 'movement': ''},
    'gauntlet:the-marrow-entrance': {'name': 'The Marrow Entrance', 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:the-marrow-northeast': {'name': 'The Marrow Northeast', 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:shellwood-east': {'name': 'Shellwood East', 'needle': 0, 'dps': None, 'movement': ''},
    'gauntlet:sinner-s-road-northeast': {'name': "Sinner's Road Northeast", 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:the-slab-east': {'name': 'The Slab East', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:the-slab-northwest': {'name': 'The Slab Northwest', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:disgraced-chef-lugoli': {'name': 'Disgraced Chef Lugoli', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:bilehaven-groal-the-great': {'name': 'Bilehaven (Groal the Great)', 'needle': 4, 'dps': 'high', 'movement': ''},
    'gauntlet:choral-chambers-west': {'name': 'Choral Chambers West', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:cogwork-core-southwest': {'name': 'Cogwork Core Southwest', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:cogwork-core-northeast': {'name': 'Cogwork Core Northeast', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:cogwork-core-southeast': {'name': 'Cogwork Core Southeast', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:skull-cavern': {'name': 'Skull Cavern', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:halfway-home': {'name': 'Halfway Home', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:high-halls-gauntlet': {'name': 'High Halls Gauntlet', 'needle': 2, 'dps': 'mid', 'movement': ''},
    'gauntlet:memorium-northeast': {'name': 'Memorium Northeast', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:broodmother-gauntlet': {'name': 'Broodmother Gauntlet', 'needle': 2, 'dps': 'mid', 'movement': ''},
    'gauntlet:underworks-northwest': {'name': 'Underworks Northwest', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:underworks-center': {'name': 'Underworks Center', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:the-cauldron-gauntlet': {'name': 'The Cauldron Gauntlet', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:whispering-vaults-entrance': {'name': 'Whispering Vaults Entrance', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:whispering-vaults-entrance-2': {'name': 'Whispering Vaults Entrance #2', 'needle': 2, 'dps': 'mid', 'movement': ''},
    'gauntlet:balm-for-the-wounded-gauntlet': {'name': 'Balm for the Wounded Gauntlet', 'needle': 1, 'dps': None, 'movement': ''},
    'gauntlet:the-unravelled-gauntlet': {'name': 'The Unravelled Gauntlet', 'needle': 2, 'dps': 'mid', 'movement': ''},
    'gauntlet:karmelita-gauntlet': {'name': 'Karmelita Gauntlet', 'needle': 4, 'dps': 'high', 'movement': ''},
    'gauntlet:black-threaded-skarrgard': {'name': 'Black-Threaded Skarrgard', 'needle': 2, 'dps': 'mid', 'movement': ''},
    'gauntlet:greymoor-tower-act-3': {'name': 'Greymoor Tower', 'needle': 2, 'dps': 'mid', 'movement': ''},
    'gauntlet:crawfather-gauntlet': {'name': 'Crawfather Gauntlet', 'needle': 2, 'dps': 'mid', 'movement': ''},
    'gauntlet:high-halls-gauntlet-2': {'name': 'High Halls Gauntlet #2', 'needle': 2, 'dps': 'mid', 'movement': ''},
    'gauntlet:hunter-s-march-center-2': {'name': "Hunter's March Center #2", 'needle': 2, 'dps': 'mid', 'movement': ''},
    'gauntlet:coral-tower-all-4': {'name': 'Coral Tower (All 4)', 'needle': 4, 'dps': 'high', 'movement': ''},
    'gauntlet:shellwood-west': {'name': 'Shellwood West', 'needle': 2, 'dps': 'mid', 'movement': ''},
    'gauntlet:lost-verdania-center': {'name': 'Lost Verdania Center', 'needle': 2, 'dps': 'mid', 'movement': ''},
}


def profiles():
    return _PROFILES


def requirement_name(key):
    if key.startswith('group:'):
        tier, kind = key.removeprefix('group:').split(':')
        if tier not in TIERS or kind not in ('skills', 'tools', 'either'):
            raise ValueError(f'Unknown combat group: {key}')
        return f'{tier.title()} DPS ' + {'skills': 'Skills', 'tools': 'Tools', 'either': 'Skills/Tools'}[kind]
    profile = profiles()[key]
    suffix = ' (Act 3)' if key.endswith('-act-3') else ''
    return f"Combat: {profile['name']}{' Gauntlet' if key.startswith('gauntlet:') and 'gauntlet' not in profile['name'].lower() else ''}{suffix}"


def build_requirements(req, item_count, crests, tier_reduction=0):
    result = {}
    skill_crests = tuple(sorted(c for c in crests if c != 'Crest: Architect'))
    for skill in (s for skills in SKILLS_BY_TIER.values() for s in skills):
        result['Combat Skill: ' + skill] = tuple(
            req(skill, crest, crest=False) for crest in skill_crests
        )
    for tool in (t for tools in TOOLS_BY_TIER.values() for t in tools):
        if tool in ('Curveclaw', 'Curvesickle'):
            result['Combat Tool: ' + tool] = tuple(
                req(crest, crest=False, item_counts=(item_count(
                    2 if tool == 'Curvesickle' else 1, 'Progressive Curveclaw'),))
                for crest in sorted(crests) if crest != 'Crest: Shaman'
            )
        else:
            result['Combat Tool: ' + tool] = (req('Usable ' + tool, crest=False),)
    for index, tier in enumerate(TIERS):
        skills = [req('Combat Skill: ' + skill, crest=False)
                  for level in TIERS[index:] for skill in SKILLS_BY_TIER[level]]
        if index:
            skills.extend(req('Volt Skill: ' + skill, crest=False)
                          for skill in SKILLS_BY_TIER[TIERS[index - 1]])
        result[requirement_name('group:' + tier + ':skills')] = tuple(skills)
        result[requirement_name('group:' + tier + ':tools')] = tuple(
            req('Combat Tool: ' + tool, crest=False)
            for level in TIERS[index:] for tool in TOOLS_BY_TIER[level]
        )
        result[requirement_name('group:' + tier + ':either')] = tuple(
            req(requirement_name('group:' + tier + ':' + kind), crest=False)
            for kind in ('skills', 'tools')
        )
    result['Very High DPS Skills'] = tuple(
        req('Volt Skill: ' + skill, crest=False) for skill in SKILLS_BY_TIER['high']
    )
    movement_items = {'Dash': 'Ancestral Art: Swift Step', 'Faydown': 'Ability: Faydown Cloak',
                      "Drifter's": "Ability: Drifter's Cloak", 'Clawline': 'Ancestral Art: Clawline'}
    for key, profile in profiles().items():
        name = requirement_name(key)
        needle = max(0, profile['needle'] - tier_reduction)
        damage = [req(crest=False, item_counts=(item_count(needle, 'Progressive Needle Upgrade'),)
                      if needle else ())]
        if profile['dps']:
            for reduction, group in ((1, 'Mid DPS Skills/Tools'), (2, 'High DPS Skills/Tools'),
                                     (3, 'Very High DPS Skills')):
                if reduction > needle:
                    continue
                if tier_reduction:
                    group = {'Mid DPS Skills/Tools': 'Low DPS Skills/Tools',
                             'High DPS Skills/Tools': 'Mid DPS Skills/Tools',
                             'Very High DPS Skills': 'High DPS Skills'}[group]
                damage.append(req(group, crest=False, item_counts=(
                    (item_count(needle - reduction, 'Progressive Needle Upgrade'),)
                    if needle > reduction else ())))
        result[name + ' Damage'] = tuple(damage)
        groups = [part.strip().strip('()').strip() for part in profile['movement'].split(' AND ') if part]
        alternatives = [()]
        for group in groups:
            options = [movement_items[item.strip()] for item in group.split(' OR ')]
            alternatives = [(*left, option) for left in alternatives for option in options]
        result[name] = (
            req('Option: Proficient Combat'),
            *(req(name + ' Damage', *movement) for movement in alternatives),
        )
    return result
