package it.sculptura.matrixandroid;

import android.app.Activity;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.provider.Settings;
import android.view.Gravity;
import android.view.View;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.SeekBar;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

public class MainActivity extends Activity {
    private SharedPreferences prefs;
    private SeekBar speed, density, size, fps;
    private Spinner mode;

    @Override protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        prefs = PreferenceManager.getDefaultSharedPreferences(this);
        setContentView(buildUi());
    }

    private View buildUi() {
        ScrollView scroll = new ScrollView(this);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(36, 42, 36, 42);
        root.setBackgroundColor(Color.rgb(5, 8, 5));
        scroll.addView(root);

        TextView title = text("MATRIX ANDROID", 28, Color.rgb(70, 255, 120));
        title.setGravity(Gravity.CENTER_HORIZONTAL);
        root.addView(title);
        TextView sub = text("Screensaver configurabile per Android", 15, Color.LTGRAY);
        sub.setGravity(Gravity.CENTER_HORIZONTAL);
        root.addView(sub);

        root.addView(label("Preset"));
        LinearLayout presets = new LinearLayout(this);
        presets.setOrientation(LinearLayout.HORIZONTAL);
        presets.addView(presetButton("Eco", 42, 48, 34, 20));
        presets.addView(presetButton("Bilanciato", 70, 75, 30, 30));
        presets.addView(presetButton("Massimo", 95, 95, 24, 60));
        root.addView(presets);

        root.addView(label("Tipo di codice"));
        mode = new Spinner(this);
        String[] labels = {"Solo numeri 0–9", "Binario 0/1", "Esadecimale", "Misto"};
        ArrayAdapter<String> a = new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, labels);
        mode.setAdapter(a);
        String saved = prefs.getString("mode", "digits");
        mode.setSelection(saved.equals("binary") ? 1 : saved.equals("hex") ? 2 : saved.equals("mixed") ? 3 : 0);
        mode.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            public void onItemSelected(AdapterView<?> p, View v, int pos, long id) {
                String val = pos == 1 ? "binary" : pos == 2 ? "hex" : pos == 3 ? "mixed" : "digits";
                prefs.edit().putString("mode", val).apply();
            }
            public void onNothingSelected(AdapterView<?> p) {}
        });
        root.addView(mode);

        speed = addSeek(root, "Velocità", "speed", 30, 120, 70);
        density = addSeek(root, "Densità", "density", 25, 100, 75);
        size = addSeek(root, "Dimensione caratteri", "size", 18, 56, 30);
        fps = addSeek(root, "Fluidità FPS", "fps", 20, 60, 30);

        Button preview = button("ANTEPRIMA");
        preview.setOnClickListener(v -> {
            saveSeekValues();
            setContentView(new MatrixRainView(this));
            Toast.makeText(this, "Tocca Indietro per tornare alle impostazioni", Toast.LENGTH_SHORT).show();
        });
        root.addView(preview);

        Button settings = button("APRI IMPOSTAZIONI SALVASCHERMO");
        settings.setOnClickListener(v -> {
            saveSeekValues();
            try {
                startActivity(new Intent(Settings.ACTION_DREAM_SETTINGS));
            } catch (Exception e) {
                Toast.makeText(this, "Apri Impostazioni > Schermo > Salvaschermo", Toast.LENGTH_LONG).show();
            }
        });
        root.addView(settings);

        TextView note = text("Dopo l'installazione seleziona “Matrix Android” come salvaschermo di sistema. Modalità iniziale: solo numeri.", 14, Color.GRAY);
        note.setPadding(0, 24, 0, 12);
        root.addView(note);
        return scroll;
    }

    @Override public void onBackPressed() {
        setContentView(buildUi());
    }

    private Button presetButton(String name, int spd, int den, int sz, int f) {
        Button b = button(name);
        b.setOnClickListener(v -> {
            prefs.edit().putInt("speed", spd).putInt("density", den).putInt("size", sz).putInt("fps", f).apply();
            if (speed != null) speed.setProgress(spd - 30);
            if (density != null) density.setProgress(den - 25);
            if (size != null) size.setProgress(sz - 18);
            if (fps != null) fps.setProgress(f - 20);
        });
        return b;
    }

    private SeekBar addSeek(LinearLayout root, String title, String key, int min, int max, int def) {
        TextView l = label(title);
        root.addView(l);
        SeekBar s = new SeekBar(this);
        s.setMax(max - min);
        s.setProgress(prefs.getInt(key, def) - min);
        s.setTag(min);
        root.addView(s);
        return s;
    }

    private void saveSeekValues() {
        prefs.edit()
            .putInt("speed", speed.getProgress() + (Integer)speed.getTag())
            .putInt("density", density.getProgress() + (Integer)density.getTag())
            .putInt("size", size.getProgress() + (Integer)size.getTag())
            .putInt("fps", fps.getProgress() + (Integer)fps.getTag())
            .apply();
    }

    private TextView label(String s) {
        TextView t = text(s, 17, Color.WHITE);
        t.setPadding(0, 28, 0, 8);
        return t;
    }

    private TextView text(String s, int sp, int color) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextSize(sp);
        t.setTextColor(color);
        return t;
    }

    private Button button(String s) {
        Button b = new Button(this);
        b.setText(s);
        b.setAllCaps(false);
        return b;
    }
}
