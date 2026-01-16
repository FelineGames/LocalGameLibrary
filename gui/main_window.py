from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout,
    QLabel, QPushButton, QScrollArea, QLineEdit
)
from PySide6.QtCore import Qt

from backend.game_manager import load_library, save_library
from gui.add_game_dialog import AddGameDialog
from gui.game_card import GameCard


class MainWindow(QWidget):
    def __init__(self):
        super().__init__()

        self.setWindowTitle("Local Games Library")
        self.resize(1920, 1080)

        self.games = load_library()

        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(0, 0, 0, 0)
        main_layout.setSpacing(0)

        top_bar = QWidget()
        top_bar.setFixedHeight(55)
        top_bar.setStyleSheet("background:#171D25;")

        top_layout = QHBoxLayout(top_bar)
        top_layout.setContentsMargins(20, 0, 20, 0)

        title = QLabel("Library")
        title.setStyleSheet("color:white;font-size:20px;font-weight:bold;")

        self.search = QLineEdit()
        self.search.setPlaceholderText("Search games...")
        self.search.setFixedHeight(32)
        self.search.setStyleSheet("""
            QLineEdit {
                background: #FFFFFF;
                color: black;
                border: none;
                border-radius: 6px;
                padding: 6px 10px;
            }
        """)
        self.search.textChanged.connect(self.apply_search)

        add_btn = QPushButton("+ Add Game")
        add_btn.setStyleSheet("""
            QPushButton {
                color: white;
                background: #1F2F50;
                padding: 6px 14px;
                border-radius: 4px;
            }
            QPushButton:hover { background: #2b406a; }
        """)
        add_btn.clicked.connect(self.add_game)

        top_layout.addWidget(title)
        top_layout.addStretch()
        top_layout.addWidget(self.search)
        top_layout.addWidget(add_btn)

        library_area = QWidget()
        library_area.setStyleSheet("background:#1F2F50;")

        self.library_layout = QVBoxLayout(library_area)
        self.library_layout.setAlignment(Qt.AlignTop)
        self.library_layout.setContentsMargins(20, 20, 20, 20)
        self.library_layout.setSpacing(15)

        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setWidget(library_area)
        scroll.setFrameShape(QScrollArea.NoFrame)

        scroll.setStyleSheet("""
            QScrollArea {
                background: #1F2F50;
                border: none;
            }
            QScrollArea > QWidget {
                background: #1F2F50;
            }
            QScrollArea > QWidget > QWidget {
                background: #1F2F50;
            }
        """)

        main_layout.addWidget(top_bar)
        main_layout.addWidget(scroll)

        self.refresh()

    def refresh(self):
        while self.library_layout.count():
            item = self.library_layout.takeAt(0)
            if item.widget():
                item.widget().deleteLater()

        for game in self.games:
            card = GameCard(game, self)
            self.library_layout.addWidget(card)

    def add_game(self):
        dialog = AddGameDialog(self)
        if dialog.exec():
            self.games.append(dialog.game_data)
            save_library(self.games)
            self.refresh()

    def delete_game(self, game):
        self.games = [g for g in self.games if g != game]
        save_library(self.games)
        self.refresh()

    def apply_search(self, text):
        text = text.lower()

        while self.library_layout.count():
            item = self.library_layout.takeAt(0)
            if item.widget():
                item.widget().deleteLater()

        for game in self.games:
            if text in game["title"].lower() or text in game["author"].lower():
                card = GameCard(game, self)
                self.library_layout.addWidget(card)
