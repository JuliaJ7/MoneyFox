﻿using System;
using System.Linq;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Java.Lang.Reflect;
using MoneyFox.Droid.Renderer;
using NLog;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using Color = Xamarin.Forms.Color;
using Object = Java.Lang.Object;

[assembly: ExportRenderer(typeof(SearchBar), typeof(CustomSearchBarRenderer))]

namespace MoneyFox.Droid.Renderer
{
    public class CustomSearchBarRenderer : SearchBarRenderer
    {
        public CustomSearchBarRenderer(Context context) : base(context)
        {
        }

        protected override void OnElementChanged(ElementChangedEventArgs<SearchBar> e)
        {
            base.OnElementChanged(e);
            if (Control != null)
            {
                SearchView searchView = Control;
                searchView.Iconified = false;
                searchView.SetIconifiedByDefault(false);

                EditText editText = Control.GetChildrenOfType<EditText>().First();

                editText.SetHighlightColor(Color.Accent.ToAndroid());

                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
                    TrySetCursorPointerColorNew(editText);
                }
                else
                {
                    TrySetCursorPointerColor(editText);
                }

                UpdateSearchButtonColor();
                UpdateCancelButtonColor();
            }
        }

        private static void TrySetCursorPointerColorNew(EditText editText)
        {
            try
            {
                editText.SetTextCursorDrawable(Resource.Drawable.CustomCursor);

                var textSelectHandleDrawable = editText.TextSelectHandle;
                textSelectHandleDrawable.SetColorFilter(new BlendModeColorFilter(Color.Accent.ToAndroid(), BlendMode.SrcIn));
                editText.TextSelectHandle = textSelectHandleDrawable;

                var textSelectHandleLeftDrawable = editText.TextSelectHandleLeft;
                textSelectHandleLeftDrawable.SetColorFilter(new BlendModeColorFilter(Color.Accent.ToAndroid(), BlendMode.SrcIn));
                editText.TextSelectHandle = textSelectHandleLeftDrawable;

                var textSelectHandleRightDrawable = editText.TextSelectHandleRight;
                textSelectHandleRightDrawable.SetColorFilter(new BlendModeColorFilter(Color.Accent.ToAndroid(), BlendMode.SrcIn));
                editText.TextSelectHandle = textSelectHandleRightDrawable;
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Error(ex);
            }
        }

        private void TrySetCursorPointerColor(EditText editText)
        {
            try
            {
                var textViewTemplate = new TextView(editText.Context);

                Field field = textViewTemplate.Class.GetDeclaredField("mEditor");
                field.Accessible = true;
                Object editor = field.Get(editText);

                string[] fieldsNames =
                    {"mTextSelectHandleLeftRes", "mTextSelectHandleRightRes", "mTextSelectHandleRes"};
                string[] drawableNames = {"mSelectHandleLeft", "mSelectHandleRight", "mSelectHandleCenter"};

                for (var index = 0; index < fieldsNames.Length && index < drawableNames.Length; index++)
                {
                    string fieldName = fieldsNames[index];
                    string drawableName = drawableNames[index];

                    field = textViewTemplate.Class.GetDeclaredField(fieldName);
                    field.Accessible = true;
                    int handle = field.GetInt(editText);

                    Drawable handleDrawable = Resources.GetDrawable(handle, null);

                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                    {
                        handleDrawable.SetColorFilter(new BlendModeColorFilter(Color.Accent.ToAndroid(), BlendMode.SrcIn));
                    }
                    else
                    {
                        handleDrawable.SetColorFilter(Color.Accent.ToAndroid(), PorterDuff.Mode.SrcIn);
                    }

                    field = editor.Class.GetDeclaredField(drawableName);
                    field.Accessible = true;
                    field.Set(editor, handleDrawable);
                }

                IntPtr intPtrtextViewClass = JNIEnv.FindClass(typeof(TextView));
                IntPtr mCursorDrawableResProperty = JNIEnv.GetFieldID(intPtrtextViewClass, "mCursorDrawableRes", "I");

                JNIEnv.SetField(editText.Handle, mCursorDrawableResProperty, Resource.Drawable.CustomCursor);
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Error(ex);
            }
        }

        private void UpdateSearchButtonColor()
        {
            int searchViewCloseButtonId = Control.Resources.GetIdentifier("android:id/search_mag_icon", null, null);
            if (searchViewCloseButtonId != 0)
            {
                SetColorGray(FindViewById<ImageView>(searchViewCloseButtonId));
            }
        }

        private void UpdateCancelButtonColor()
        {
            int searchViewCloseButtonId = Control.Resources.GetIdentifier("android:id/search_close_btn", null, null);
            if (searchViewCloseButtonId != 0)
            {
                SetColorGray(FindViewById<ImageView>(searchViewCloseButtonId));
            }
        }

        private static void SetColorGray(ImageView image)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                image?.Drawable?.SetColorFilter(new BlendModeColorFilter(Android.Graphics.Color.Gray, BlendMode.SrcIn));
            }
            else
            {
                image?.Drawable?.SetColorFilter(Android.Graphics.Color.Gray, PorterDuff.Mode.SrcIn);
            }
        }
    }
}
