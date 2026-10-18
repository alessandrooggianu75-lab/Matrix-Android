package it.sculptura.matrixandroid;

import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.preference.PreferenceManager;
import android.view.View;

import java.util.Random;

public class MatrixRainView extends View {
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Random random = new Random();
    private float[] y;
    private int columns;
    private int charSize;
    private float speedPx;
    private float density;
    private int fps;
    private String alphabet;
    private long frameDelay;

    public MatrixRainView(Context context) {
        super(context);
        paint.setTypeface(Typeface.MONOSPACE);
        setBackgroundColor(Color.rgb(2, 6, 3));
        loadPrefs();
    }

    private void loadPrefs() {
        SharedPreferences p = PreferenceManager.getDefaultSharedPreferences(getContext());
        charSize = p.getInt("size", 30);
        speedPx = p.getInt("speed", 70) / 10f;
        density = p.getInt("density", 75) / 100f;
        fps = p.getInt("fps", 30);
        frameDelay = Math.max(16, 1000L / Math.max(20, fps));
        String mode = p.getString("mode", "digits");
        if ("binary".equals(mode)) alphabet = "01";
        else if ("hex".equals(mode)) alphabet = "0123456789ABCDEF";
        else if ("mixed".equals(mode)) alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        else alphabet = "0123456789";
        paint.setTextSize(charSize);
    }

    @Override protected void onSizeChanged(int w, int h, int oldw, int oldh) {
        columns = Math.max(1, (int)(w / (charSize * 0.78f)));
        y = new float[columns];
        for (int i = 0; i < columns; i++) y[i] = random.nextInt(Math.max(1, h));
    }

    @Override protected void onDraw(Canvas c) {
        super.onDraw(c);
        if (y == null) return;

        paint.setColor(Color.argb(38, 0, 0, 0));
        c.drawRect(0, 0, getWidth(), getHeight(), paint);
        paint.setTextSize(charSize);

        float colWidth = getWidth() / (float) columns;
        for (int i = 0; i < columns; i++) {
            if (random.nextFloat() > density) continue;
            char ch = alphabet.charAt(random.nextInt(alphabet.length()));
            int green = 190 + random.nextInt(66);
            paint.setColor(Color.rgb(40, green, 90));
            if (random.nextFloat() < 0.06f) paint.setColor(Color.rgb(210, 255, 220));
            c.drawText(String.valueOf(ch), i * colWidth, y[i], paint);
            y[i] += speedPx + random.nextFloat() * speedPx * 0.5f;
            if (y[i] > getHeight() + charSize && random.nextFloat() > 0.96f) {
                y[i] = -random.nextInt(Math.max(charSize, getHeight() / 3));
            }
        }
        postInvalidateDelayed(frameDelay);
    }
}
