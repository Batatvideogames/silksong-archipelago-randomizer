from __future__ import annotations
from .combat_logic_data import BOSSES, GAUNTLETS

MASK_SHARD_ITEMS = ('Mask Shard',)

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

_PROFILES = {}
_PROFILES.update(BOSSES)
_PROFILES.update(GAUNTLETS)

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


def build_requirements(req, item_count, crests, bind_threshold=1):
    result = {'Capability: Bind': (req(),)}
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
    for index, tier in enumerate(TIERS):
        result['Volt ' + tier.title() + ' DPS Skills'] = tuple(
            req('Volt Skill: ' + skill, crest=False)
            for level in TIERS[index:] for skill in SKILLS_BY_TIER[level]
        )
    result['Combat Sprint'] = (req(crest=False, any_of=('Swift Step', 'Progressive Swift Step')),)
    movement_items = {'Dash': 'Ancestral Art: Swift Step', 'Faydown': 'Ability: Faydown Cloak',
                      "Drifter's": "Ability: Drifter's Cloak", 'Clawline': 'Ancestral Art: Clawline',
                      'Sprint': 'Combat Sprint', 'Cling Grip': 'Ancestral Art: Cling Grip',
                      'Magma Bell': 'Usable Magma Bell'}
    for key, profile in profiles().items():
        name = requirement_name(key)
        needle = profile['needle']
        damage = [req(crest=needle > 0, item_counts=(item_count(needle, 'Progressive Needle Upgrade'),)
                      if needle else ())]
        if profile['dps']:
            def add_damage(reduction, tier, kind, boosted=False):
                if reduction > profile['needle']:
                    return
                level = tier
                group = ('Volt ' + level.title() + ' DPS Skills' if boosted and kind == 'skills'
                         else requirement_name('group:' + level + ':' + kind))
                counts = []
                if needle > reduction:
                    counts.append(item_count(needle - reduction, 'Progressive Needle Upgrade'))
                if boosted and kind == 'tools':
                    counts.append(item_count(3, 'Progressive Crafting Kit'))
                damage.append(req(group, crest=False, item_counts=tuple(counts)))

            add_damage(1, 'mid', 'either')
            add_damage(2, 'mid', 'skills', boosted=True)
            add_damage(2, 'mid', 'tools', boosted=True)
            if profile['dps'] == 'high':
                add_damage(2, 'high', 'either')
                add_damage(3, 'high', 'skills', boosted=True)
                add_damage(3, 'high', 'tools', boosted=True)
        result[name + ' Damage'] = tuple(damage)
        movement = profile['movement']
        groups = [part.strip().strip('()').strip() for part in movement.split(' AND ') if part]
        alternatives = [()]
        for group in groups:
            options = [movement_items[item.strip()] for item in group.split(' OR ')]
            alternatives = [(*left, option) for left in alternatives for option in options]
        mask_count = profile['masks']
        masks = (item_count(mask_count, *MASK_SHARD_ITEMS),) if mask_count else ()
        bind = ('Capability: Bind',) if needle >= bind_threshold else ()
        result[name] = (
            req('Option: Proficient Combat', crest=False),
            *(req(name + ' Damage', *movement, *bind, crest=False, item_counts=masks)
              for movement in alternatives),
        )
    return result
