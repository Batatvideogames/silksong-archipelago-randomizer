from unittest import TestCase

from test.general import setup_multiworld

from .. import SilksongWorld
from ..items import PROGRESSIVE_SILK_HEART_ITEM


class TestEarlySilkHeart(TestCase):
    @staticmethod
    def get_early_items(**options: str) -> dict[str, int]:
        multiworld = setup_multiworld(
            SilksongWorld,
            steps=("generate_early",),
            options=options,
        )
        return multiworld.local_early_items[1]

    def test_anywhere_skills_leave_opening_checks_for_movement(self) -> None:
        early_items = self.get_early_items(
            skill_randomization="anywhere",
            silk_heart_randomization="anywhere",
        )

        self.assertNotIn(PROGRESSIVE_SILK_HEART_ITEM, early_items)


    def test_vanilla_skill_or_heart_does_not_request_one(self) -> None:
        for options in (
            {
                "skill_randomization": "vanilla",
                "silk_heart_randomization": "anywhere",
            },
            {
                "skill_randomization": "anywhere",
                "silk_heart_randomization": "vanilla",
            },
        ):
            with self.subTest(**options):
                early_items = self.get_early_items(**options)
                self.assertNotIn(PROGRESSIVE_SILK_HEART_ITEM, early_items)

